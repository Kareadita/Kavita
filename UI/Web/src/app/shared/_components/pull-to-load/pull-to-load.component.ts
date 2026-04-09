import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  OnInit,
  output,
  signal,
  viewChild
} from '@angular/core';
import {DOCUMENT} from "@angular/common";


/** How long (ms) the user must be idle at the scroll boundary before scroll-driven progress arms. */
const SCROLL_ARM_DELAY_MS = 300;

/** Rubber-band ease-out duration (ms) when the user releases before reaching 100%. */
const RELEASE_ANIMATION_MS = 180;

/** Brief highlight duration (ms) after the trigger fires before resetting. */
const TRIGGER_FLASH_MS = 200;

/** Maximum scaleY multiplier applied to the container at 100% progress. */
const MAX_SCALE_Y = 3;

/** Resting height of the strip in px - kept as a constant so layout is predictable. */
const RESTING_HEIGHT_PX = 20;

/** IntersectionObserver threshold – component must be fully visible before tracking begins. */
const VISIBILITY_THRESHOLD = 1.0;

/** Minimum pointer movement (px) in the drag axis before we start tracking, to avoid jitter. */
const POINTER_DEAD_ZONE_PX = 4;

/**
 * How long (ms) after the last wheel event we wait before considering the
 * scroll gesture "released" and rubber-banding back. Wheel events have no
 * clean "end" signal like pointerup/touchend, so we rely on inactivity.
 */
const SCROLL_RELEASE_DELAY_MS = 150;

const enum PullState {
  Idle,
  Armed,
  Dragging,
  Triggered,
}

@Component({
  selector: 'app-pull-to-load',
  imports: [],
  templateUrl: './pull-to-load.component.html',
  styleUrl: './pull-to-load.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.--ptl-release-ms]': 'releaseMsToken',
    '[style.--ptl-flash-ms]': 'flashMsToken',
  },
})
export class PullToLoadComponent implements OnInit {
  /** Which direction the user drags to trigger – "up" places the strip at the bottom of content. */
  readonly direction = input.required<'up' | 'down'>();

  /** Drag distance required to reach 100%, expressed in vh units. */
  readonly distance = input.required<number>();

  /** Label shown inside the strip (e.g. "Read Next Chapter"). */
  readonly title = input.required<string>();

  /** Scroll container to monitor. Falls back to the document body when omitted. */
  readonly scrollContainer = input<ElementRef | HTMLElement | undefined>(undefined);

  /** Suppresses all tracking when true. */
  readonly disabled = input<boolean>(false);

  /** Fires once when the user completes the pull gesture (progress reaches 100%). */
  readonly triggered = output<void>();

  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly container = viewChild.required<ElementRef<HTMLElement>>('container');

  private readonly progress = signal(0);
  private readonly state = signal(PullState.Idle);

  readonly isReleasing = signal(false);
  readonly isTriggered = computed(() => this.state() === PullState.Triggered);

  readonly releaseMsToken = `${RELEASE_ANIMATION_MS}ms`;
  readonly flashMsToken = `${TRIGGER_FLASH_MS}ms`;

  readonly progressPercent = computed(() => Math.min(this.progress() * 100, 100));
  readonly restingHeight = RESTING_HEIGHT_PX;

  readonly containerTransform = computed(() => {
    const scale = 1 + this.progress() * (MAX_SCALE_Y - 1);
    return `scaleY(${scale})`;
  });

  readonly transformOrigin = computed(() =>
    this.direction() === 'up' ? 'top center' : 'bottom center'
  );

  private activePointerId: number | null = null;
  private pointerStartY = 0;
  private pointerPassedDeadZone = false;

  private armTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private scrollAccumulator = 0;
  private scrollReleaseTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private lastTouchY: number | null = null;

  private observer: IntersectionObserver | null = null;
  private baseListeners: Array<() => void> = [];
  private armedListeners: Array<() => void> = [];

  ngOnInit(): void {
    this.setupVisibilityObserver();

    this.destroyRef.onDestroy(() => {
      this.teardownAllTracking();
      this.observer?.disconnect();
    });
  }

  private setupVisibilityObserver(): void {
    this.observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.some(e => e.intersectionRatio >= VISIBILITY_THRESHOLD);
        if (visible && !this.disabled()) {
          this.setupBaseTracking();
        } else {
          this.teardownAllTracking();
        }
      },
      { threshold: VISIBILITY_THRESHOLD }
    );

    queueMicrotask(() => {
      this.observer!.observe(this.container().nativeElement);
    });
  }

  /**
   * Base layer: pointer events on the strip itself (always intentional, no arming needed)
   * and passive scroll events on the container to detect when the user reaches the boundary.
   */
  private setupBaseTracking(): void {
    if (this.baseListeners.length > 0) return;

    const el = this.container().nativeElement;
    const scrollEl = this.resolveScrollElement();

    const onPointerDown = (e: PointerEvent) => this.onPointerDown(e);
    const onPointerMove = (e: PointerEvent) => this.onPointerMove(e);
    const onPointerUp = (e: PointerEvent) => this.onPointerUp(e);
    const onScroll = () => this.onScroll(scrollEl);

    el.addEventListener('pointerdown', onPointerDown);
    el.addEventListener('pointermove', onPointerMove);
    el.addEventListener('pointerup', onPointerUp);
    el.addEventListener('pointercancel', onPointerUp);

    const scrollTarget = this.getScrollEventTarget(scrollEl);
    scrollTarget.addEventListener('scroll', onScroll, { passive: true });

    this.baseListeners.push(
      () => el.removeEventListener('pointerdown', onPointerDown),
      () => el.removeEventListener('pointermove', onPointerMove),
      () => el.removeEventListener('pointerup', onPointerUp),
      () => el.removeEventListener('pointercancel', onPointerUp),
      () => scrollTarget.removeEventListener('scroll', onScroll),
    );

    this.evaluateScrollArming(scrollEl);
  }

  /**
   * Armed layer: wheel and touchmove listeners attached only after the arm timer
   * completes. These fire even when the browser has no remaining scroll distance,
   * which is how we drive progress past the scroll boundary.
   *
   * Both use { passive: false } so we can preventDefault to suppress the browser's
   * overscroll bounce while the user is driving progress.
   */
  private setupArmedTracking(): void {
    if (this.armedListeners.length > 0) return;

    const scrollEl = this.resolveScrollElement();
    const target = this.getScrollEventTarget(scrollEl);

    const onWheel = ((e: WheelEvent) => this.onWheel(e)) as EventListener;
    const onTouchStart = ((e: TouchEvent) => this.onTouchStart(e)) as EventListener;
    const onTouchMove = ((e: TouchEvent) => this.onTouchMove(e)) as EventListener;
    const onTouchEnd = (() => this.onScrollGestureEnd()) as EventListener;

    target.addEventListener('wheel', onWheel, { passive: false });
    target.addEventListener('touchstart', onTouchStart, { passive: true });
    target.addEventListener('touchmove', onTouchMove, { passive: false });
    target.addEventListener('touchend', onTouchEnd, { passive: true });
    target.addEventListener('touchcancel', onTouchEnd, { passive: true });

    this.armedListeners.push(
      () => target.removeEventListener('wheel', onWheel),
      () => target.removeEventListener('touchstart', onTouchStart),
      () => target.removeEventListener('touchmove', onTouchMove),
      () => target.removeEventListener('touchend', onTouchEnd),
      () => target.removeEventListener('touchcancel', onTouchEnd),
    );
  }

  private teardownArmedTracking(): void {
    for (const remove of this.armedListeners) remove();
    this.armedListeners = [];
    this.clearScrollReleaseTimeout();
    this.lastTouchY = null;
  }

  private teardownAllTracking(): void {
    for (const remove of this.baseListeners) remove();
    this.baseListeners = [];
    this.teardownArmedTracking();
    this.clearArmTimeout();
    this.resetDragState();
  }

  private onPointerDown(e: PointerEvent): void {
    if (this.state() === PullState.Triggered || this.disabled()) return;
    if (this.activePointerId !== null) return;

    this.activePointerId = e.pointerId;
    this.pointerStartY = e.clientY;
    this.pointerPassedDeadZone = false;
    this.container().nativeElement.setPointerCapture(e.pointerId);
  }

  private onPointerMove(e: PointerEvent): void {
    if (e.pointerId !== this.activePointerId) return;

    const deltaY = this.pointerStartY - e.clientY;
    const signedDelta = this.direction() === 'up' ? deltaY : -deltaY;

    if (!this.pointerPassedDeadZone) {
      if (Math.abs(deltaY) < POINTER_DEAD_ZONE_PX) return;
      this.pointerPassedDeadZone = true;
      this.state.set(PullState.Dragging);
      this.isReleasing.set(false);
    }

    const clamped = Math.max(0, signedDelta);
    const thresholdPx = this.distanceInPx();
    const p = Math.min(clamped / thresholdPx, 1);
    this.progress.set(p);

    if (p >= 1) {
      this.fire();
    }
  }

  private onPointerUp(e: PointerEvent): void {
    if (e.pointerId !== this.activePointerId) return;
    this.activePointerId = null;

    if (this.state() === PullState.Dragging) {
      this.releaseWithRubberBand();
    }
  }

  private onScroll(scrollEl: HTMLElement | Window): void {
    if (this.state() === PullState.Triggered || this.disabled()) return;

    // User scrolled away from the boundary while armed/dragging – disarm and reset
    if (this.state() === PullState.Dragging || this.state() === PullState.Armed) {
      if (!this.isAtScrollBoundary(scrollEl)) {
        this.teardownArmedTracking();
        this.scrollAccumulator = 0;
        this.progress.set(0);
        this.state.set(PullState.Idle);
      }
      return;
    }

    this.evaluateScrollArming(scrollEl);
  }

  /**
   * Starts the arming countdown when the user sits at the scroll boundary.
   * Resets on continued scroll activity so programmatic scroll-to-position
   * (which blows through the boundary without pausing) never arms.
   */
  private evaluateScrollArming(scrollEl: HTMLElement | Window): void {
    if (!this.isAtScrollBoundary(scrollEl)) {
      this.clearArmTimeout();
      return;
    }

    this.clearArmTimeout();
    this.armTimeoutId = setTimeout(() => {
      if (this.isAtScrollBoundary(scrollEl) && !this.disabled()) {
        this.state.set(PullState.Armed);
        this.scrollAccumulator = 0;
        this.setupArmedTracking();
      }
    }, SCROLL_ARM_DELAY_MS);
  }

  private onWheel(e: WheelEvent): void {
    if (this.state() !== PullState.Armed && this.state() !== PullState.Dragging) return;

    const delta = this.direction() === 'up' ? e.deltaY : -e.deltaY;
    if (delta <= 0) return;

    e.preventDefault();
    this.accumulateScrollDelta(delta);
    this.scheduleScrollRelease();
  }

  private onTouchStart(e: TouchEvent): void {
    if (e.touches.length === 1) {
      this.lastTouchY = e.touches[0].clientY;
    }
  }

  private onTouchMove(e: TouchEvent): void {
    if (this.state() !== PullState.Armed && this.state() !== PullState.Dragging) return;
    if (this.lastTouchY === null || e.touches.length !== 1) return;

    const currentY = e.touches[0].clientY;
    const rawDelta = this.lastTouchY - currentY;
    this.lastTouchY = currentY;

    const delta = this.direction() === 'up' ? rawDelta : -rawDelta;
    if (delta <= 0) return;

    e.preventDefault();
    this.accumulateScrollDelta(delta);
    this.scheduleScrollRelease();
  }

  private onScrollGestureEnd(): void {
    this.clearScrollReleaseTimeout();
    this.lastTouchY = null;

    if (this.state() === PullState.Dragging) {
      this.releaseWithRubberBand();
    }
  }

  private accumulateScrollDelta(delta: number): void {
    this.scrollAccumulator += delta;
    this.state.set(PullState.Dragging);
    this.isReleasing.set(false);

    const thresholdPx = this.distanceInPx();
    const p = Math.min(this.scrollAccumulator / thresholdPx, 1);
    this.progress.set(p);

    if (p >= 1) {
      this.fire();
    }
  }

  /**
   * Wheel events have no "end" signal like pointerup/touchend. We treat
   * inactivity beyond this timeout as a release and rubber-band back.
   */
  private scheduleScrollRelease(): void {
    this.clearScrollReleaseTimeout();
    this.scrollReleaseTimeoutId = setTimeout(
      () => this.onScrollGestureEnd(),
      SCROLL_RELEASE_DELAY_MS,
    );
  }

  private fire(): void {
    this.state.set(PullState.Triggered);
    this.activePointerId = null;
    this.teardownArmedTracking();
    this.triggered.emit();

    setTimeout(() => {
      this.progress.set(0);
      this.state.set(PullState.Idle);
    }, TRIGGER_FLASH_MS);
  }

  /** Snaps progress back to 0 with a CSS transition but stays Armed since the user is still at the boundary. */
  private releaseWithRubberBand(): void {
    this.isReleasing.set(true);
    this.progress.set(0);
    this.scrollAccumulator = 0;
    this.state.set(PullState.Armed);

    setTimeout(() => this.isReleasing.set(false), RELEASE_ANIMATION_MS);
  }

  private distanceInPx(): number {
    return (this.distance() / 100) * this.document.documentElement.clientHeight;
  }

  private resolveScrollElement(): HTMLElement | Window {
    const ref = this.scrollContainer();
    if (!ref) return window;
    return ref instanceof ElementRef ? ref.nativeElement : ref;
  }

  /**
   * Wheel and touch listeners need to attach to the document when using window
   * as the scroll container, since window doesn't receive wheel events directly.
   */
  private getScrollEventTarget(scrollEl: HTMLElement | Window): HTMLElement | Document {
    return scrollEl instanceof Window ? this.document : scrollEl;
  }

  private isAtScrollBoundary(scrollEl: HTMLElement | Window): boolean {
    const { scrollTop, scrollHeight, clientHeight } = this.getScrollMetrics(scrollEl);

    if (this.direction() === 'up') {
      return scrollTop + clientHeight >= scrollHeight - 1;
    }
    return scrollTop <= 1;
  }

  private getScrollMetrics(scrollEl: HTMLElement | Window): {
    scrollTop: number;
    scrollHeight: number;
    clientHeight: number;
  } {
    if (scrollEl instanceof Window) {
      return {
        scrollTop: this.document.documentElement.scrollTop,
        scrollHeight: this.document.documentElement.scrollHeight,
        clientHeight: this.document.documentElement.clientHeight,
      };
    }
    return {
      scrollTop: scrollEl.scrollTop,
      scrollHeight: scrollEl.scrollHeight,
      clientHeight: scrollEl.clientHeight,
    };
  }

  private clearArmTimeout(): void {
    if (this.armTimeoutId !== null) {
      clearTimeout(this.armTimeoutId);
      this.armTimeoutId = null;
    }
  }

  private clearScrollReleaseTimeout(): void {
    if (this.scrollReleaseTimeoutId !== null) {
      clearTimeout(this.scrollReleaseTimeoutId);
      this.scrollReleaseTimeoutId = null;
    }
  }

  private resetDragState(): void {
    this.activePointerId = null;
    this.progress.set(0);
    this.state.set(PullState.Idle);
    this.scrollAccumulator = 0;
    this.lastTouchY = null;
  }

}
