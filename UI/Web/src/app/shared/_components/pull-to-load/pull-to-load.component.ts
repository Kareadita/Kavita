import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import {DOCUMENT} from '@angular/common';
import {BreakpointService} from '../../../_services/breakpoint.service';

/** How long (ms) the user must be idle at the scroll boundary before scroll-driven progress arms. */
const SCROLL_ARM_DELAY_MS = 100;

/** Resting height of the strip before arming. */
const RESTING_HEIGHT_REM = 1.25;

/** Expanded height when armed on desktop, giving the user a scroll-through region. */
const ARMED_HEIGHT_DESKTOP_REM = 18.75;

/** Expanded height when armed on mobile — shorter drag distance for touch. */
const ARMED_HEIGHT_MOBILE_REM = 9.375;

const enum PullState {
  Idle,
  Armed,
  Triggered,
}

@Component({
  selector: 'app-pull-to-load',
  imports: [],
  templateUrl: './pull-to-load.component.html',
  styleUrl: './pull-to-load.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PullToLoadComponent {
  /** Which direction the user drags to trigger – "up" places the strip at the bottom of content. */
  readonly direction = input.required<'up' | 'down'>();

  /** Label shown inside the strip (e.g. "Read Next Chapter"). */
  readonly title = input.required<string>();

  /** Scroll container to monitor. Falls back to the document body when omitted. */
  readonly scrollContainer = input<ElementRef | HTMLElement | undefined>(undefined);

  /** Suppresses all tracking when true. */
  readonly disabled = input<boolean>(false);

  /** Fires once when the user completes the scroll-through (trigger sentinel fully visible). */
  readonly triggered = output<void>();

  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly breakpointService = inject(BreakpointService);
  private readonly container = viewChild.required<ElementRef<HTMLElement>>('container');
  private readonly triggerSentinel = viewChild.required<ElementRef<HTMLElement>>('triggerSentinel');

  private readonly state = signal(PullState.Idle);
  readonly progress = signal(0);

  readonly isArmed = computed(() => this.state() === PullState.Armed);
  readonly isTriggered = computed(() => this.state() === PullState.Triggered);

  readonly progressPercent = computed(() => Math.min(this.progress() * 100, 100));
  readonly restingHeightRem = RESTING_HEIGHT_REM;

  readonly armedHeightRem = computed(() =>
    this.breakpointService.isMobile() ? ARMED_HEIGHT_MOBILE_REM : ARMED_HEIGHT_DESKTOP_REM
  );

  readonly containerHeight = computed(() =>
    this.isArmed() || this.isTriggered() ? `${this.armedHeightRem()}rem` : `${RESTING_HEIGHT_REM}rem`
  );
  readonly directionArrow = computed(() => {
    switch (this.direction()) {
      case 'down': return 'up';
      case 'up': return 'down';
    }
  });

  private armTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private fireTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private scrollListener: (() => void) | null = null;
  private isCompensatingScroll = false;

  constructor() {
    effect(() => {
      // Track only scrollContainer — untracked prevents disarm/setup from
      // registering state()/direction() as effect dependencies.
      this.scrollContainer();
      untracked(() => {
        this.disarm();
        this.setupScrollListener();
      });
    });

    this.destroyRef.onDestroy(() => {
      this.teardown();
    });
  }

  /**
   * Attaches a single scroll listener that drives the entire state machine:
   * Idle: checks if the resting-height container is fully visible -> arms after delay
   * Armed: tracks scroll-through progress -> fires when sentinel is visible
   */
  private setupScrollListener() {
    this.teardownScrollListener();

    const scrollEl = this.resolveScrollElement();
    const scrollTarget = scrollEl instanceof Window ? this.document.body : scrollEl;

    const onScroll = () => this.onScroll();
    scrollTarget.addEventListener('scroll', onScroll, {passive: true});
    this.scrollListener = () => scrollTarget.removeEventListener('scroll', onScroll);
  }

  private onScroll() {
    if (this.disabled() || this.isCompensatingScroll) return;

    const currentState = this.state();

    if (currentState === PullState.Triggered) return;

    if (currentState === PullState.Idle) {
      this.checkVisibilityForArming();
    } else if (currentState === PullState.Armed) {
      this.updateProgress();
      this.checkTrigger();
      this.checkDisarm();
    }
  }

  /**
   * In Idle state: check if the container (at resting height) is fully visible
   * in the scroll container using getBoundingClientRect. If so, start the arm countdown.
   * Uses rect checks instead of IntersectionObserver to avoid issues with
   * ancestor CSS transforms (e.g. translate3d for hardware acceleration).
   */
  private checkVisibilityForArming() {
    if (this.isFullyVisible(this.container().nativeElement)) {
      if (this.armTimeoutId === null) {
        this.startArmCountdown();
      }
    } else {
      this.clearArmTimeout();
    }
  }

  /**
   * After SCROLL_ARM_DELAY_MS of being fully visible, arm the component:
   * expand to full height and start tracking scroll progress.
   */
  private startArmCountdown() {
    this.clearArmTimeout();

    this.armTimeoutId = setTimeout(() => {
      if (this.disabled() || this.state() === PullState.Triggered) return;

      // Re-check visibility in case user scrolled away during the delay
      if (!this.isFullyVisible(this.container().nativeElement)) return;

      this.state.set(PullState.Armed);
      this.progress.set(0);

      // When direction is 'down' (top spacer), the expansion pushes content downward.
      // Wait for Angular to update the DOM height, then compensate scroll position
      // so the user's view stays on the content with just the text bar visible.
      if (this.direction() === 'down') {
        requestAnimationFrame(() => this.adjustScrollTop(this.getExpansionDeltaPx()));
      }
    }, SCROLL_ARM_DELAY_MS);
  }

  /**
   * In Armed state: compute progress as the fraction of the expanded container
   * that is visible within the scroll container.
   */
  private updateProgress() {
    const el = this.container().nativeElement;
    const rect = el.getBoundingClientRect();
    const [boundsTop, boundsBottom] = this.getScrollBounds();

    let visibleHeight;
    if (this.direction() === 'up') {
      visibleHeight = Math.max(0, Math.min(boundsBottom - rect.top, rect.height));
    } else {
      visibleHeight = Math.max(0, Math.min(rect.bottom - boundsTop, rect.height));
    }

    const p = rect.height > 0 ? Math.min(visibleHeight / rect.height, 1) : 0;
    this.progress.set(p);
  }

  /**
   * In Armed state: check if the trigger sentinel at the far end of the
   * expanded container is fully visible. If so, the user has scrolled through.
   */
  private checkTrigger() {
    if (this.isFullyVisible(this.triggerSentinel().nativeElement)) {
      this.fire();
    }
  }

  /**
   * In Armed state: if the container is no longer even partially visible
   * within the scroll container, the user scrolled away — disarm and shrink back.
   */
  private checkDisarm() {
    const rect = this.container().nativeElement.getBoundingClientRect();
    const [boundsTop, boundsBottom] = this.getScrollBounds();

    if (rect.bottom < boundsTop || rect.top > boundsBottom) {
      this.disarm();
    }
  }

  private fire() {
    if (this.state() === PullState.Triggered) return;

    this.state.set(PullState.Triggered);
    this.progress.set(1);
    this.triggered.emit();

    // Reset after a brief flash so the component is ready for next use
    this.fireTimeoutId = setTimeout(() => {
      this.progress.set(0);
      this.state.set(PullState.Idle);
    }, 200);
  }

  private disarm() {
    this.clearArmTimeout();

    if (this.state() !== PullState.Triggered) {
      const wasArmed = this.state() === PullState.Armed;
      this.state.set(PullState.Idle);
      this.progress.set(0);

      if (wasArmed && this.direction() === 'down') {
        this.adjustScrollTop(-this.getExpansionDeltaPx());
      }
    }
  }

  private getExpansionDeltaPx() {
    const rootFontSize = parseFloat(getComputedStyle(this.document.documentElement).fontSize) || 16;
    return (this.armedHeightRem() - RESTING_HEIGHT_REM) * rootFontSize;
  }

  /**
   * Adjusts scrollTop by a fixed amount. Sets a guard flag so the resulting
   * scroll events are ignored and don't re-trigger state changes.
   * The guard lasts two animation frames to cover the scroll event dispatch.
   */
  private adjustScrollTop(deltaPx: number) {
    this.isCompensatingScroll = true;

    const scrollEl = this.resolveScrollElement();
    if (scrollEl instanceof Window) {
      const current = this.document.documentElement.scrollTop || this.document.body.scrollTop;
      const target = Math.max(0, current + deltaPx);
      this.document.documentElement.scrollTop = target;
      this.document.body.scrollTop = target;
    } else {
      scrollEl.scrollTop = Math.max(0, scrollEl.scrollTop + deltaPx);
    }

    // Two rAF hops: first for the browser to process the scroll, second to
    // ensure any resulting scroll event has been dispatched and ignored.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        this.isCompensatingScroll = false;
      });
    });
  }

  /**
   * Checks whether an element is fully visible within the scroll container
   * using getBoundingClientRect. For non-window scroll containers, coordinates
   * are compared against the container's bounds rather than the browser viewport.
   * Immune to ancestor CSS transforms that break IntersectionObserver.
   */
  private isFullyVisible(el: HTMLElement): boolean {
    const rect = el.getBoundingClientRect();
    const [boundsTop, boundsLeft, boundsBottom, boundsRight] = this.getVisibleBounds();

    // 2px tolerance accounts for sub-pixel rounding from translate3d hardware
    // acceleration and fractional rem sizing of the trigger sentinel.
    const tolerance = 2;

    return rect.top >= boundsTop - tolerance
      && rect.left >= boundsLeft - tolerance
      && rect.bottom <= boundsBottom + tolerance
      && rect.right <= boundsRight + tolerance
      && rect.height > 0;
  }

  /**
   * Returns [top, left, bottom, right] bounds of the scroll container in
   * viewport coordinates. For window this is the viewport rect; for a custom
   * element it's that element's bounding client rect.
   */
  private getVisibleBounds(): [number, number, number, number] {
    const scrollEl = this.resolveScrollElement();
    if (scrollEl instanceof Window) {
      return [0, 0, this.document.documentElement.clientHeight, this.document.documentElement.clientWidth];
    }
    const r = scrollEl.getBoundingClientRect();
    return [r.top, r.left, r.bottom, r.right];
  }

  /**
   * Returns [top, bottom] of the scroll container's visible area in viewport coordinates.
   */
  private getScrollBounds(): [number, number] {
    const b = this.getVisibleBounds();
    return [b[0], b[2]];
  }

  /**
   * Resolves the actual scroll element. Normalizes `undefined` and `document.body`
   * to `window` so that scroll listeners attach to `document` (where page-level
   * scroll events actually fire) and viewport math uses consistent coordinates.
   */
  private resolveScrollElement(): HTMLElement | Window {
    const ref = this.scrollContainer();
    if (!ref) return window;
    const el = ref instanceof ElementRef ? ref.nativeElement : ref;
    return el === this.document.body ? window : el;
  }

  private clearArmTimeout() {
    if (this.armTimeoutId !== null) {
      clearTimeout(this.armTimeoutId);
      this.armTimeoutId = null;
    }
  }

  private clearFireTimeout() {
    if (this.fireTimeoutId !== null) {
      clearTimeout(this.fireTimeoutId);
      this.fireTimeoutId = null;
    }
  }

  private teardownScrollListener() {
    if (this.scrollListener) {
      this.scrollListener();
      this.scrollListener = null;
    }
  }

  private teardown() {
    this.teardownScrollListener();
    this.clearArmTimeout();
    this.clearFireTimeout();
  }
}
