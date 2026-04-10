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
import {BreakpointService} from "../../../_services/breakpoint.service";

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
export class PullToLoadComponent implements OnInit {
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
  private scrollListener: (() => void) | null = null;
  private isCompensatingScroll = false;


  ngOnInit(): void {
    this.setupScrollListener();

    this.destroyRef.onDestroy(() => {
      this.teardown();
    });
  }

  /**
   * Attaches a single scroll listener that drives the entire state machine:
   * Idle: checks if the resting-height container is fully visible -> arms after delay
   * Armed: tracks scroll-through progress -> fires when sentinel is visible
   */
  private setupScrollListener(): void {
    this.teardownScrollListener();

    const scrollEl = this.resolveScrollElement();
    const scrollTarget = scrollEl instanceof Window ? this.document : scrollEl;

    const onScroll = () => this.onScroll();
    scrollTarget.addEventListener('scroll', onScroll, {passive: true});
    this.scrollListener = () => scrollTarget.removeEventListener('scroll', onScroll);
  }

  private onScroll(): void {
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
   * in the viewport using getBoundingClientRect. If so, start the arm countdown.
   * Uses rect checks instead of IntersectionObserver to avoid issues with
   * ancestor CSS transforms (e.g. translate3d for hardware acceleration).
   */
  private checkVisibilityForArming(): void {
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
  private startArmCountdown(): void {
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
   * that is visible in the viewport.
   */
  private updateProgress(): void {
    const el = this.container().nativeElement;
    const rect = el.getBoundingClientRect();
    const viewportHeight = this.getViewportHeight();

    let visibleHeight: number;
    if (this.direction() === 'up') {
      visibleHeight = Math.max(0, Math.min(viewportHeight - rect.top, rect.height));
    } else {
      visibleHeight = Math.max(0, Math.min(rect.bottom, rect.height));
    }

    const p = rect.height > 0 ? Math.min(visibleHeight / rect.height, 1) : 0;
    this.progress.set(p);
  }

  /**
   * In Armed state: check if the trigger sentinel at the far end of the
   * expanded container is fully visible. If so, the user has scrolled through.
   */
  private checkTrigger(): void {
    if (this.isFullyVisible(this.triggerSentinel().nativeElement)) {
      this.fire();
    }
  }

  /**
   * In Armed state: if the container is no longer even partially visible,
   * the user scrolled away, disarm and shrink back.
   */
  private checkDisarm(): void {
    const rect = this.container().nativeElement.getBoundingClientRect();
    const viewportHeight = this.getViewportHeight();

    // If the entire container is above the viewport or below it, disarm
    if (rect.bottom < 0 || rect.top > viewportHeight) {
      this.disarm();
    }
  }

  private fire(): void {
    if (this.state() === PullState.Triggered) return;

    this.state.set(PullState.Triggered);
    this.progress.set(1);
    this.triggered.emit();

    // Reset after a brief flash so the component is ready for next use
    setTimeout(() => {
      this.progress.set(0);
      this.state.set(PullState.Idle);
    }, 200);
  }

  private disarm(): void {
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

  private getExpansionDeltaPx(): number {
    const rootFontSize = parseFloat(getComputedStyle(this.document.documentElement).fontSize) || 16;
    return (this.armedHeightRem() - RESTING_HEIGHT_REM) * rootFontSize;
  }

  /**
   * Adjusts scrollTop by a fixed amount. Sets a guard flag so the resulting
   * scroll events are ignored and don't re-trigger state changes.
   * The guard lasts two animation frames to cover the scroll event dispatch.
   */
  private adjustScrollTop(deltaPx: number): void {
    this.isCompensatingScroll = true;

    const scrollEl = this.resolveScrollElement();
    if (scrollEl instanceof Window || scrollEl === this.document.body) {
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
   * Checks whether an element is fully visible in the viewport using getBoundingClientRect.
   * Immune to ancestor CSS transforms that break IntersectionObserver.
   */
  private isFullyVisible(el: HTMLElement): boolean {
    const rect = el.getBoundingClientRect();
    const viewportHeight = this.getViewportHeight();
    const viewportWidth = this.document.documentElement.clientWidth;

    return rect.top >= 0
      && rect.left >= 0
      && rect.bottom <= viewportHeight
      && rect.right <= viewportWidth
      && rect.height > 0;
  }

  private getViewportHeight(): number {
    const scrollEl = this.resolveScrollElement();
    // For window or body, use the visual viewport height.
    // For other elements (e.g. fullscreen reader), use the element's client height.
    if (scrollEl instanceof Window || scrollEl === this.document.body) {
      return this.document.documentElement.clientHeight;
    }
    return scrollEl.clientHeight;
  }

  private resolveScrollElement(): HTMLElement | Window {
    const ref = this.scrollContainer();
    if (!ref) return window;
    return ref instanceof ElementRef ? ref.nativeElement : ref;
  }

  private clearArmTimeout(): void {
    if (this.armTimeoutId !== null) {
      clearTimeout(this.armTimeoutId);
      this.armTimeoutId = null;
    }
  }

  private teardownScrollListener(): void {
    if (this.scrollListener) {
      this.scrollListener();
      this.scrollListener = null;
    }
  }

  private teardown(): void {
    this.teardownScrollListener();
    this.clearArmTimeout();
  }
}
