import {ElementRef, inject, Injectable} from '@angular/core';
import {NavigationEnd, Router} from '@angular/router';
import {filter, ReplaySubject} from 'rxjs';

interface ScrollEndOptions {
  tolerance?: number;
  timeout?: number;
  debounce?: number;
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

  private activeScrollHandlers = new Map<HTMLElement, {
    timeoutId?: number;
    callback?: () => void;
    targetPosition?: { x?: number; y?: number };
    tolerance: number;
  }>();

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

  scrollTo(position: number, element: HTMLElement, behavior: 'auto' | 'smooth' = 'smooth',
           onComplete?: () => void, options?: ScrollEndOptions) {

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
    const tolerance = options?.tolerance ?? 5;
    const timeout = options?.timeout ?? 3000;
    const debounce = options?.debounce ?? 100;

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
    const handlerData = {
      callback,
      targetPosition,
      tolerance,
      timeoutId: window.setTimeout(() => {
        console.warn('Scroll completion timeout reached - forcing completion');
        this.executeCallback(element, callback);
      }, timeout)
    };

    this.activeScrollHandlers.set(element, handlerData);
    element.addEventListener('scroll', scrollHandler, { passive: true });

    (handlerData as any).cleanup = () => {
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

  private executeCallback(element: HTMLElement, callback: () => void): void {
    this.clearScrollHandler(element);

    try {
      callback();
    } catch (error) {
      console.error('Error in scroll completion callback:', error);
    }
  }

  private clearScrollHandler(element: HTMLElement): void {
    const handler = this.activeScrollHandlers.get(element);
    if (handler) {
      if ((handler as any).cleanup) {
        (handler as any).cleanup();
      }
      this.activeScrollHandlers.delete(element);
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

  private debugLog(message: string, extraData?: any) {
    if (!this.debugMode) return;

    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }
}
