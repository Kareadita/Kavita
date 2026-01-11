import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {DOCUMENT} from "@angular/common";
import {fromEvent, merge, startWith} from "rxjs";
import {debounceTime} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

export enum Breakpoint {
  Mobile = 768,
  Tablet = 1280,
  Desktop = 1440
}

interface BreakpointThresholds {
  mobile: number;
  tablet: number;
}

const DEFAULT_THRESHOLDS: BreakpointThresholds = {
  mobile: Breakpoint.Mobile,
  tablet: Breakpoint.Tablet
};

const CSS_VAR_MOBILE = '--setting-mobile-breakpoint';
const CSS_VAR_TABLET = '--setting-tablet-breakpoint';

@Injectable({
  providedIn: 'root'
})
export class BreakpointService {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly DEBOUNCE_MS = 60;

  private readonly windowWidth = signal<number>(this.getWindowWidth());

  /**
   * Thresholds are re-evaluated on resize to support dynamic theme changes.
   * Reads from CSS custom properties, falls back to Breakpoint enum defaults.
   */
  private readonly thresholds = computed<BreakpointThresholds>(() => {
    this.windowWidth(); // Reactive dependency
    return this.resolveThresholds();
  });

  readonly activeBreakpoint = computed<Breakpoint>(() => {
    const width = this.windowWidth();
    const t = this.thresholds();

    if (width <= t.mobile) return Breakpoint.Mobile;
    if (width <= t.tablet) return Breakpoint.Tablet;
    return Breakpoint.Desktop;
  });

  readonly isMobile = computed(() => this.activeBreakpoint() === Breakpoint.Mobile);
  readonly isTablet = computed(() => this.activeBreakpoint() === Breakpoint.Tablet);
  readonly isDesktop = computed(() => this.activeBreakpoint() === Breakpoint.Desktop);
  readonly isMobileOrBelow = computed(() => this.activeBreakpoint() <= Breakpoint.Mobile);
  readonly isMobileOrAbove = computed(() => this.activeBreakpoint() >= Breakpoint.Mobile); // Always true
  readonly isAboveMobile = computed(() => this.activeBreakpoint() > Breakpoint.Mobile);
  readonly isTabletOrBelow = computed(() => this.activeBreakpoint() <= Breakpoint.Tablet);
  readonly isTabletOrAbove = computed(() => this.activeBreakpoint() >= Breakpoint.Tablet);
  readonly isDesktopOrBelow = computed(() => this.activeBreakpoint() <= Breakpoint.Desktop); // Always true
  readonly isDesktopOrAbove = computed(() => this.activeBreakpoint() >= Breakpoint.Desktop);
  readonly currentWidth = this.windowWidth.asReadonly();

  constructor() {
    this.initResizeListener();
  }

  private initResizeListener(): void {
    const window = this.document.defaultView;
    if (!window) return;

    merge(
      fromEvent(window, 'resize'),
      fromEvent(window, 'orientationchange')
    ).pipe(
      debounceTime(this.DEBOUNCE_MS),
      startWith(null),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.windowWidth.set(this.getWindowWidth());
    });
  }

  private getWindowWidth(): number {
    const window = this.document.defaultView;
    if (!window) return DEFAULT_THRESHOLDS.tablet + 1; // SSR fallback: desktop

    return window.innerWidth
      || this.document.documentElement?.clientWidth
      || this.document.body?.clientWidth
      || DEFAULT_THRESHOLDS.tablet + 1;
  }

  private resolveThresholds(): BreakpointThresholds {
    const style = this.getComputedBodyStyle();
    if (!style) return DEFAULT_THRESHOLDS;

    return {
      mobile: this.parseCssVar(style, CSS_VAR_MOBILE, DEFAULT_THRESHOLDS.mobile),
      tablet: this.parseCssVar(style, CSS_VAR_TABLET, DEFAULT_THRESHOLDS.tablet)
    };
  }

  private getComputedBodyStyle(): CSSStyleDeclaration | null {
    const window = this.document.defaultView;
    if (!window || !this.document.body) return null;
    return window.getComputedStyle(this.document.body);
  }

  private parseCssVar(style: CSSStyleDeclaration, property: string, fallback: number): number {
    const value = style.getPropertyValue(property)?.trim();
    if (!value) return fallback;

    const parsed = parseInt(value, 10);
    return isNaN(parsed) ? fallback : parsed;
  }
}
