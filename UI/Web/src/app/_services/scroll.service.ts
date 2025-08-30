import {ElementRef, inject, Injectable, signal} from '@angular/core';
import {NavigationEnd, Router} from '@angular/router';
import {filter, ReplaySubject} from 'rxjs';

const DEFAULT_TIMEOUT = 3000;
const DEFAULT_TOLERANCE = 3;
const DEFAULT_DEBOUNCE = 100;

interface ScrollEndOptions {
  tolerance?: number;
  timeout?: number;
  debounce?: number;
}

interface ScrollToOptions {
  scrollIntoViewOptions: ScrollIntoViewOptions;
  timeout: number;
}

interface ScrollHandler {
  timeoutId?: number;
  callback?: () => void;
  targetPosition?: { x?: number; y?: number };
  tolerance: number;
  cleanup?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class ScrollService {

  private readonly router = inject(Router);

  private readonly debugMode = false;

  private readonly scrollContainerSource =  new ReplaySubject<string | ElementRef<HTMLElement>>(1);
  /**
   * Exposes the current container on the active screen that is our primary overlay area. Defaults to 'body' and changes to 'body' on page loads
   */
  public readonly scrollContainer$ = this.scrollContainerSource.asObservable();

  private activeScrollHandlers = new Map<HTMLElement, ScrollHandler>();

  private readonly _lock = signal(false);
  public readonly isScrollingLock = this._lock.asReadonly();

  constructor() {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.scrollContainerSource.next('body');
        this.cleanup();
      });
    this.scrollContainerSource.next('body');
  }

  get scrollPosition() {
    return (window.pageYOffset
      || document.documentElement.scrollTop
      || document.body.scrollTop || 0);
  }

  /*
   * When in the scroll vertical position the scroll in the horizontal position is needed
   */
  get scrollPositionX() {
    return (window.pageXOffset
      || document.documentElement.scrollLeft
      || document.body.scrollLeft || 0);
  }

  /**
   * Returns true if the log is active
   * @private
   */
  private checkLock(): boolean {

    return false; // NOTE: We don't need locking anymore - it bugs out

    if (!this._lock()) return false;

    console.warn("[ScrollService] tried to scroll while locked, timings should be checked")

    return true;
  }

  private intersectionObserver(element: HTMLElement, callback?: () => void) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.executeCallback(element, callback);
        }
      });
    }, {threshold: 1.0});

    observer.observe(element);
    return observer;
  }

  scrollIntoView(element: HTMLElement, options?: ScrollToOptions, callback?: () => void) {
    if (this.checkLock()) return;
    this._lock.set(true);

    const timeoutId = window.setTimeout(() => {
      console.warn('Intersection observer timed out - forcing callback execution');
      this.executeCallback(element, callback)
    }, DEFAULT_TIMEOUT)

    const observer = this.intersectionObserver(element, callback);
    const scrollHandler: ScrollHandler = {
      timeoutId: timeoutId,
      callback: callback,
      tolerance: 0,
      cleanup: () => {
        observer.disconnect();
        observer.unobserve(element);
        clearTimeout(timeoutId);
      },
    }

    this.activeScrollHandlers.set(element, scrollHandler);
    if (options?.timeout || 0) {
      setTimeout(() => element.scrollIntoView(options?.scrollIntoViewOptions), options?.timeout || 0);
    } else {
      element.scrollIntoView(options?.scrollIntoViewOptions);
    }

  }

  scrollTo(position: number, element: HTMLElement, behavior: 'auto' | 'smooth' = 'smooth',
           onComplete?: () => void, options?: ScrollEndOptions) {
    if (this.checkLock()) return;
    this._lock.set(true);

    element.scrollTo({
      top: position,
      behavior: behavior
    });

    if (onComplete) {
      this.onScrollEnd((element as HTMLElement), onComplete, { y: position }, options);
    }
  }

  scrollToX(position: number, element: HTMLElement, behavior: 'auto' | 'smooth' = 'auto',
            onComplete?: () => void, options?: ScrollEndOptions) {
    if (this.checkLock()) return;
    this._lock.set(true);

    element.scrollTo({
      left: position,
      behavior: behavior
    });

    if (onComplete) {
      this.onScrollEnd((element as HTMLElement), onComplete, { x: position }, options);
    }
  }

  setScrollContainer(elem: ElementRef<HTMLElement> | undefined) {
    if (elem !== undefined) {
      this.scrollContainerSource.next(elem);
    }
  }

  /**
   * Register scroll end callback
   */
  private onScrollEnd(
    element: HTMLElement,
    callback: () => void,
    targetPosition?: { x?: number; y?: number },
    options?: ScrollEndOptions
  ): void {
    const tolerance = options?.tolerance ?? DEFAULT_TOLERANCE;
    const timeout = options?.timeout ?? DEFAULT_TIMEOUT;
    const debounce = options?.debounce ?? DEFAULT_DEBOUNCE;

    this.clearScrollHandler(element);

    let debounceTimer: number;
    let scrollEventCount = 0;

    const checkComplete = () => {
      const currentX = element.scrollLeft;
      const currentY = element.scrollTop;

      if (targetPosition) {
        let isComplete = true;
        let deltaInfo: any = {};

        if (targetPosition.x !== undefined) {
          const deltaX = Math.abs(currentX - targetPosition.x);
          deltaInfo.deltaX = deltaX;
          if (deltaX > tolerance) {
            isComplete = false;
          }
        }
        if (targetPosition.y !== undefined) {
          const deltaY = Math.abs(currentY - targetPosition.y);
          deltaInfo.deltaY = deltaY;
          if (deltaY > tolerance) {
            isComplete = false;
          }
        }

        this.debugLog('Completion check:', {
          isComplete,
          ...deltaInfo,
          tolerance
        });

        if (isComplete) {
          this.debugLog('Scroll completed successfully');
          this.executeCallback(element, callback);
          return;
        }
      }
    };

    const scrollHandler = () => {
      scrollEventCount++;
      this.debugLog(`Scroll event #${scrollEventCount}`);

      clearTimeout(debounceTimer);
      debounceTimer = window.setTimeout(() => {
        this.debugLog('Scroll debounce timeout reached');
        checkComplete();

        if (!targetPosition) {
          this.debugLog('No target position - completing');
          this.executeCallback(element, callback);
        }
      }, debounce);
    };

    // Rest of your existing scroll handler setup...
    const handlerData: ScrollHandler = {
      callback,
      targetPosition,
      tolerance,
      timeoutId: window.setTimeout(() => {
        this.executeCallback(element, callback);
      }, timeout)
    };

    this.activeScrollHandlers.set(element, handlerData);
    element.addEventListener('scroll', scrollHandler, { passive: true });

    handlerData.cleanup = () => {
      this.debugLog('Cleaning up scroll handler');
      element.removeEventListener('scroll', scrollHandler);
      clearTimeout(debounceTimer);
      if (handlerData.timeoutId) {
        clearTimeout(handlerData.timeoutId);
      }
    };

    // Check immediately for instant scrolls
    setTimeout(() => {
      this.debugLog('Initial completion check');
      checkComplete();
    }, 50);
  }

  private executeCallback(element: HTMLElement, callback?: () => void): void {
    this._lock.set(false);

    this.clearScrollHandler(element);

    if (!callback) return;

    try {
      callback();
    } catch (error) {
      console.error('Error in scroll completion callback:', error);
    }
  }

  private clearScrollHandler(element: HTMLElement): void {
    const handler = this.activeScrollHandlers.get(element);
    if (!handler) return;

    this.activeScrollHandlers.delete(element);
    if (handler.cleanup) {
      handler.cleanup();
    }
  }

  /**
   * Clean up all handlers
   */
  cleanup(): void {
    this.activeScrollHandlers.forEach((handler, element) => {
      this.clearScrollHandler(element);
    });
  }

  /**
   * Force unlocking of scroll lock
   */
  unlock() {
    this._lock.set(false);
    this.cleanup();
  }

  private debugLog(message: string, extraData?: any) {
    if (!this.debugMode) return;

    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }
}
