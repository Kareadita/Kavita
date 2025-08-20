import {computed, Injectable, OnDestroy, Signal, signal} from '@angular/core';
import {BookPageLayoutMode} from "../_models/readers/book-page-layout-mode";
import {WritingStyle} from "../_models/preferences/writing-style";
import {EpubReaderSettingsService} from "./epub-reader-settings.service";

export interface LayoutMeasurements {
  windowWidth: number,
  windowHeight: number,
  contentWidth: number,
  contentHeight: number,
  scrollWidth: number,
  scrollHeight: number,
  readerWidth: number,
  readerHeight: number,
}

/**
 * Used in Epub reader to simplify
 */
@Injectable({
  providedIn: 'root'
})
export class LayoutMeasurementService implements OnDestroy {
  private resizeObserver?: ResizeObserver;
  private intersectionObserver?: IntersectionObserver;
  private rafId?: number;
  private observedElements = new Map<string, HTMLElement>();

  // Public signals for components to consume
  readonly measurements = signal<LayoutMeasurements>({
    windowWidth: window.innerWidth,
    windowHeight: window.innerHeight,
    contentWidth: 0,
    contentHeight: 0,
    scrollWidth: 0,
    scrollHeight: 0,
    readerWidth: 0,
    readerHeight: 0,
  });

  // Settings passed in from outside to avoid circular dependency
  private layoutMode: Signal<BookPageLayoutMode> = signal<BookPageLayoutMode>(BookPageLayoutMode.Default);
  private writingStyle: Signal<WritingStyle> = signal<WritingStyle>(WritingStyle.Horizontal);


  // Computed values based on measurements and settings
  readonly virtualPageInfo = computed(() => {
    const m = this.measurements();
    const layoutMode = this.layoutMode();
    const writingStyle = this.writingStyle();

    if (layoutMode === BookPageLayoutMode.Default) {
      return { currentPage: 1, totalPages: 1, pageSize: 0 };
    }

    // Calculate based on current measurements
    const pageSize = writingStyle === WritingStyle.Vertical
      ? m.readerHeight
      : m.readerWidth;

    const totalSize = writingStyle === WritingStyle.Vertical
      ? m.scrollHeight
      : m.scrollWidth;

    const totalPages = Math.max(1, Math.ceil(totalSize / pageSize));

    return {
      currentPage: 1, // This would be calculated based on scroll position
      totalPages,
      pageSize
    };
  });

  constructor() {
    this.initializeObservers();
    this.setupWindowListeners();
  }


  updateSettings(epubReaderSettingsService: EpubReaderSettingsService) {
    this.layoutMode = epubReaderSettingsService.layoutMode;
    this.writingStyle = epubReaderSettingsService.writingStyle;
  }

  private initializeObservers(): void {
    // ResizeObserver for element size changes
    this.resizeObserver = new ResizeObserver(entries => {
      this.scheduleUpdate(() => this.handleResize(entries));
    });

    // IntersectionObserver for visibility tracking
    this.intersectionObserver = new IntersectionObserver(
      entries => this.handleIntersection(entries),
      {
        threshold: [0, 0.25, 0.5, 0.75, 1],
        rootMargin: '50px'
      }
    );
  }

  private setupWindowListeners(): void {
    // Debounced window resize handler
    // const handleWindowResize = debounce(() => {
    //   this.updateWindowMeasurements();
    // }, 150);

    window.addEventListener('resize', this.updateWindowMeasurements.bind(this));
    window.addEventListener('orientationchange', this.updateWindowMeasurements.bind(this));
  }

  /**
   * Start observing an element for size changes
   */
  observeElement(element: HTMLElement, key: string): void {
    if (this.observedElements.has(key)) {
      this.unobserveElement(key);
    }

    this.observedElements.set(key, element);
    this.resizeObserver?.observe(element);

    // Initial measurement
    this.measureElement(element, key);
  }

  /**
   * Stop observing an element
   */
  unobserveElement(key: string): void {
    const element = this.observedElements.get(key);
    if (element) {
      this.resizeObserver?.unobserve(element);
      this.observedElements.delete(key);
    }
  }

  /**
   * Observe elements for intersection (visibility)
   */
  observeForIntersection(elements: Element[]): void {
    elements.forEach(el => this.intersectionObserver?.observe(el));
  }

  private handleResize(entries: ResizeObserverEntry[]): void {
    const updates: Partial<LayoutMeasurements> = {};

    entries.forEach(entry => {
      const key = Array.from(this.observedElements.entries())
        .find(([_, el]) => el === entry.target)?.[0];

      if (!key) return;

      // Use borderBoxSize when available (more accurate)
      const size = entry.borderBoxSize?.[0] || entry.contentRect;

      switch(key) {
        case 'bookContent':
          updates.contentWidth = size.inlineSize || 0;
          updates.contentHeight = size.blockSize || 0;
          updates.scrollWidth = (entry.target as HTMLElement).scrollWidth;
          updates.scrollHeight = (entry.target as HTMLElement).scrollHeight;
          break;
        case 'readingSection':
          updates.readerWidth = size.inlineSize || 0;
          updates.readerHeight = size.blockSize || 0;
          break;
      }
    });

    this.measurements.update(current => ({ ...current, ...updates }));
  }

  private handleIntersection(entries: IntersectionObserverEntry[]): void {
    // Track which elements are visible for progress tracking
    const visibleElements = entries
      .filter(e => e.isIntersecting)
      .map(e => e.target);

    // You can emit this to another service or signal
    this.updateVisibleElements(visibleElements);
  }

  private updateVisibleElements(elements: Element[]): void {
    // Implementation for tracking visible elements
    // This could update a signal that the reader component consumes
  }

  private measureElement(element: HTMLElement, key: string): void {
    const rect = element.getBoundingClientRect();
    const updates: Partial<LayoutMeasurements> = {};

    switch(key) {
      case 'bookContent':
        updates.contentWidth = rect.width;
        updates.contentHeight = rect.height;
        updates.scrollWidth = element.scrollWidth;
        updates.scrollHeight = element.scrollHeight;
        break;
      case 'readingSection':
        updates.readerWidth = rect.width;
        updates.readerHeight = rect.height;
        break;
    }

    this.measurements.update(current => ({ ...current, ...updates }));
  }

  private updateWindowMeasurements(): void {
    this.measurements.update(current => ({
      ...current,
      windowWidth: window.innerWidth,
      windowHeight: window.innerHeight
    }));
  }

  private scheduleUpdate(callback: () => void): void {
    if (this.rafId) {
      cancelAnimationFrame(this.rafId);
    }

    this.rafId = requestAnimationFrame(() => {
      callback();
      this.rafId = undefined;
    });
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.intersectionObserver?.disconnect();
    if (this.rafId) {
      cancelAnimationFrame(this.rafId);
    }
  }
}
