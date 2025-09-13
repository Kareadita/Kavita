import {Injectable, OnDestroy, signal} from '@angular/core';

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
@Injectable()
export class LayoutMeasurementService implements OnDestroy {
  private resizeObserver?: ResizeObserver;
  private rafId?: number;
  private observedElements = new Map<string, HTMLElement>();

  private readingSectionElement?: HTMLElement;
  private bookContentElement?: HTMLElement;

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


  constructor() {
    this.initializeObservers();
    this.setupWindowListeners();
  }


  private initializeObservers() {
    // ResizeObserver for element size changes
    this.resizeObserver = new ResizeObserver(entries => {
      this.scheduleUpdate(() => this.handleResize(entries));
    });
  }

  private setupWindowListeners() {
    window.addEventListener('resize', this.updateWindowMeasurements.bind(this));
    window.addEventListener('orientationchange', this.updateWindowMeasurements.bind(this));
  }

  /**
   * Start observing an element for size changes
   */
  observeElement(element: HTMLElement, key: 'readingSection' | 'bookContent') {
    if (this.observedElements.has(key)) {
      this.unobserveElement(key);
    }

    this.observedElements.set(key, element);
    this.resizeObserver?.observe(element);

    // Store reference to key elements
    if (key === 'readingSection') {
      this.readingSectionElement = element;
    } else if (key === 'bookContent') {
      this.bookContentElement = element;
    }

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
      windowHeight: window.innerHeight,
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

    if (this.rafId) {
      cancelAnimationFrame(this.rafId);
    }
  }
}
