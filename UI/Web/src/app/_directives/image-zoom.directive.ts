import { DestroyRef, Directive, ElementRef, Input, inject } from '@angular/core';

@Directive({
  selector: '[appImageZoom]',
  standalone: true,
  host: {
    'style': 'touch-action: none; will-change: transform; transform-origin: center center;',
    '[style.transform]': 'transform',
    '[style.cursor]': 'cursor',
    '(wheel)': 'onWheel($event)',
    '(touchstart)': 'onTouchStart($event)',
    '(touchmove)': 'onTouchMove($event)',
    '(touchend)': 'onTouchEnd($event)',
    '(mousedown)': 'onMouseDown($event)',
    '(window:mousedown)': 'onWindowMouseDown($event)',
    '(window:mousemove)': 'onMouseMove($event)',
    '(window:mouseup)': 'onMouseUp()',
    '(window:touchstart)': 'onWindowTouchStart($event)',
    '(window:touchmove)': 'onWindowTouchMove($event)',
  }
})
export class ImageZoomDirective {
  /** The active instance of the image zoom directive */
  private static activeInstance: ImageZoomDirective | undefined;
  /**
   * A set of all image zoom instances.
   * There may be more than one instance when a canvas-renderer exists
   * alongside a single- or double-renderer.
   */
  private static readonly instances = new Set<ImageZoomDirective>();
  /** The document listener for handling events */
  private static listenerDocument: Document | undefined;

  /**
   * Handles wheel events on the document.
   * @param event The wheel event
   */
  private static readonly wheelListener = (event: WheelEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onWheel(event);
    }
  };

  /**
   * Handles touch start events on the document.
   * @param event The touch event
   */
  private static readonly touchStartListener = (event: TouchEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (!instance) return;

    if (event.touches.length === 2) {
      instance.onTouchStart(event);
    } else if (event.touches.length === 1 && instance.isPaginationEvent(event) && instance.isZoomedIn()) {
      const touch = event.touches[0];
      if (instance.containsPoint(touch.clientX, touch.clientY)) {
        instance.startPan(touch.clientX, touch.clientY, true);
      }
    }
  };

  /**
   * Handles touch move events on the document.
   * @param event The touch event
   */
  private static readonly touchMoveListener = (event: TouchEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onTouchMove(event);
    }
  };

  /**
   * Handles touch end events on the document.
   * @param event The touch event
   */
  private static readonly touchEndListener = (event: TouchEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onTouchEnd(event);
    }
  };

  /** A reference to the element that the directive is applied to */
  private readonly element: ElementRef<HTMLElement> = inject(ElementRef<HTMLElement>);
  /** The current zoom scale */
  private scale = 1;
  /** The current translation on the X-axis */
  private translateX = 0;
  /** The current translation on the Y-axis */
  private translateY = 0;
  /** The distance between the two fingers during a pinch gesture */
  private pinchDistance = 0;
  /** The starting X-coordinate for panning */
  private panStartX = 0;
  /** The starting Y-coordinate for panning */
  private panStartY = 0;
  /** The starting translation on the X-axis for panning */
  private panStartTranslateX = 0;
  /** The starting translation on the Y-axis for panning */
  private panStartTranslateY = 0;
  /** Whether the image is currently being panned */
  private isPanning = false;
  /** Whether the panning was started within the pagination area */
  private panStartedInPagination = false;
  /** Whether to suppress the next pagination click */
  private suppressNextPaginationClick = false;

  /** The scalar for zooming with the mouse wheel */
  private readonly zoomScalar = 0.0015;
  /** The maximum zoom level */
  private readonly maxZoomLevel = 4;

  transform = 'translate3d(0, 0, 0) scale(1)';
  cursor = 'default';

  @Input()
  set zoomResetKey(_: unknown) {
    ImageZoomDirective.activeInstance = this;
    this.reset();
  }

  /** Whether to lock scrolling when the image is zoomed in */
  @Input() lockScroll = true;
  /** Whether to recenter the image when zooming out */
  @Input() recenterOnZoomOut = true;

  /**
   * Handles mouse wheel events.
   * Zooms the image in/out when the ctrl key is pressed.
   */
  onWheel(event: WheelEvent): void {
    if (!event.ctrlKey && !event.metaKey) {
      return;
    }

    event.preventDefault();
    ImageZoomDirective.activeInstance = this;
    
    // Don't allow mouse wheel to zoom while panning
    if (this.isPanning) {
      return;
    }

    const delta = Math.exp(-event.deltaY * this.zoomScalar);
    this.setScale(this.scale * delta, event.clientX, event.clientY);
  }

  /**
   * Handles touch start events.
   * @param event The touch event
   */
  onTouchStart(event: TouchEvent): void {
    // Zoom
    if (event.touches.length === 2) {
      event.preventDefault();
      event.stopPropagation();
      ImageZoomDirective.activeInstance = this;
      this.pinchDistance = this.getDistance(event.touches[0], event.touches[1]);
      this.isPanning = false;
      return;
    }

    // Pan
    if (event.touches.length === 1 && this.isZoomedIn()) {
      this.startPan(event.touches[0].clientX, event.touches[0].clientY);
    }
  }

  /**
   * Handles touch move events.
   * @param event The touch event
   */
  onTouchMove(event: TouchEvent): void {
    // Two touches -> pinch to zoom
    if (event.touches.length === 2 && this.pinchDistance > 0) {
      event.preventDefault();
      event.stopPropagation();
      const nextDistance = this.getDistance(event.touches[0], event.touches[1]);
      const nextScale = this.scale * (nextDistance / this.pinchDistance);
      const midpoint = this.getMidpoint(event.touches[0], event.touches[1]);
      this.setScale(nextScale, midpoint.x, midpoint.y);
      this.pinchDistance = nextDistance;
      return;
    }

    // One touch -> pan
    if (event.touches.length === 1){
      if (!this.isPanning || !this.isZoomedIn()) { return; }

      event.preventDefault();
      event.stopPropagation();
      this.setPan(event.touches[0].clientX, event.touches[0].clientY);
    }
  }

  /**
   * Handles touch end events.
   * @param event The touch event
   */
  onTouchEnd(event: TouchEvent): void {
    // One touch was released, but another may still be active
    // Upon moving, the remaining touch will turn into a pan
    if (event.touches.length < 2) {
      this.pinchDistance = 0;
    }
    // The last touch was released, so panning has stopped
    if (event.touches.length === 0) {
      this.isPanning = false;
    }
  }

  /**
   * Handles mouse down events on the image.
   * Starts panning if the image has been zoomed in.
   * @param event The mouse event
   */
  onMouseDown(event: MouseEvent): void {
    if (!this.canStartPan(event.button, event.clientX, event.clientY)) {
      return;
    }

    event.preventDefault();
    this.startPan(event.clientX, event.clientY);
  }

  /**
   * Handles mouse down events on the window.
   * @param event The mouse event
   */
  onWindowMouseDown(event: MouseEvent): void {
    if (!this.isPaginationEvent(event) || !this.canStartPan(event.button, event.clientX, event.clientY)) {
      return;
    }

    event.preventDefault();
    this.startPan(event.clientX, event.clientY, true);
  }
  
  /**
   * Handles mouse move events.
   * @param event The mouse event
   */
  onMouseMove(event: MouseEvent): void {
    if (!this.isPanning) {
      return;
    }

    event.preventDefault();
    this.setPan(event.clientX, event.clientY);
  }

  /**
   * Handles touch start events on the window.
   * @param event The touch event
   */
  onWindowTouchStart(event: TouchEvent): void {
    if (event.touches.length !== 1 || !this.isPaginationEvent(event)) {
      return;
    }

    if (!this.isZoomedIn() || !this.containsPoint(event.touches[0].clientX, event.touches[0].clientY)) {
      return;
    }

    this.startPan(event.touches[0].clientX, event.touches[0].clientY, true);
  }

  /**
   * Handles touch move events on the window.
   * @param event The touch event
   * @returns 
   */
  onWindowTouchMove(event: TouchEvent): void {
    if (event.touches.length !== 1 || !this.isPanning || !this.isZoomedIn()) {
      return;
    }

    event.preventDefault();
    this.setPan(event.touches[0].clientX, event.touches[0].clientY);
  }

  /**
   * Handles mouse up events.
   */
  onMouseUp(): void {
    this.isPanning = false;
    this.cursor = this.isZoomedIn() ? 'grab' : 'default';
  }

  /**
   * Resets the zoom & pan state.
   */
  private reset(): void {
    this.scale = 1;
    this.translateX = 0;
    this.translateY = 0;
    this.pinchDistance = 0;
    this.isPanning = false;
    this.updateTransform();
  }

  /**
   * Determines whether we can start panning when a mouse down event fires.
   * Allows panning when the triggering button is the primary mouse button,
   * the image is zoomed in, and the click is within the image bounds.
   * @param button The mouse button that triggered the event
   * @param clientX The x-coordinate of the event
   * @param clientY The y-coordinate of the event
   * @returns True if we can start panning
   */
  private canStartPan(button: number, clientX: number, clientY: number): boolean {
    return button === 0 && this.isZoomedIn() && this.containsPoint(clientX, clientY);
  }

  /**
   * Determines whether the event originated from a pagination area.
   * @param event The event
   * @returns True if the event originated from a pagination area
   */
  private isPaginationEvent(event: MouseEvent | TouchEvent): boolean {
    return event.target instanceof Element && !!event.target.closest('.pagination-area');
  }

  /**
   * Determines whether the image is zoomed in.
   * @returns True when the image is zoomed in (scale > 1)
   */
  private isZoomedIn(): boolean {
    return this.scale > 1;
  }

  /**
   * Determines whether a point is within the image bounds.
   * @param clientX The x-coordinate of the point
   * @param clientY The y-coordinate of the point
   * @returns True when the point is within the image bounds
   */
  private containsPoint(clientX: number, clientY: number): boolean {
    const rect = this.element.nativeElement.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 &&
      clientX >= rect.left && clientX <= rect.right &&
      clientY >= rect.top && clientY <= rect.bottom;
  }

  /**
   * Sets the next scale value, and translates the image for a smooth zoom effect.
   * @param nextScale The next scale value
   * @param clientX The x-coordinate of the mouse/touch event
   * @param clientY The y-coordinate of the mouse/touch event
   */
  private setScale(nextScale: number, clientX: number, clientY: number): void {
    // Get the next zoom scale value
    const previousScale = this.scale;
    const nextScaleClamped = Math.max(1, Math.min(this.maxZoomLevel, nextScale));

    // Get the center of the image, so we can use it as (0,0)
    const rect = this.element.nativeElement.getBoundingClientRect();
    const layoutCenterX = (rect.left + rect.right) / 2 - this.translateX;
    const layoutCenterY = (rect.top + rect.bottom) / 2 - this.translateY;

    // Get the point on the image in its local coordinate space,
    // divide by scale to translate from screen pixels to image pixels
    const imagePointX = (clientX - layoutCenterX - this.translateX) / previousScale;
    const imagePointY = (clientY - layoutCenterY - this.translateY) / previousScale;

    // User has zoomed all the way out, reset translation
    if (nextScaleClamped === 1) {
      this.scale = 1;
      this.translateX = 0;
      this.translateY = 0;
      this.updateTransform();
      return;
    }

    this.scale = nextScaleClamped;

    // Calculate the translation needed to keep the focal point in place
    const focalTranslateX = clientX - layoutCenterX - imagePointX * nextScaleClamped;
    const focalTranslateY = clientY - layoutCenterY - imagePointY * nextScaleClamped;

    // Calculate the ratio for recentering on zoom out (no-op when zooming in)
    const zoomOutRatio = this.recenterOnZoomOut && nextScaleClamped < previousScale
      ? Math.max(0, Math.min(1, (nextScaleClamped - 1) / (previousScale - 1)))
      : 1;

    // Apply the zoom out ratio to the focal translation
    this.translateX = focalTranslateX * zoomOutRatio;
    this.translateY = focalTranslateY * zoomOutRatio;

    this.updateTransform();
  }

  /**
   * Starts panning the image.
   * @param clientX The x-coordinate of the mouse/touch event
   * @param clientY The y-coordinate of the mouse/touch event
   * @param startedInPagination Set to true if the pan event started from a pagination area
   */
  private startPan(clientX: number, clientY: number, startedInPagination = false): void {
    // Save off where the pan started from
    this.panStartX = clientX;
    this.panStartY = clientY;

    // Save off where the image was translated at the start of the pan
    this.panStartTranslateX = this.translateX;
    this.panStartTranslateY = this.translateY;

    this.isPanning = true;
    this.panStartedInPagination = startedInPagination;
    this.cursor = 'grabbing';
  }

  /**
   * Sets the panning position of the image.
   * @param clientX The x-coordinate of the mouse/touch event
   * @param clientY The y-coordinate of the mouse/touch event
   */
  private setPan(clientX: number, clientY: number): void {
    // If a touch/click started in a pagination area and the position has changed
    // (i.e., a pan), don't toggle a pagination event
    if (this.panStartedInPagination && (clientX !== this.panStartX || clientY !== this.panStartY)) {
      this.suppressNextPaginationClick = true;
    }

    this.translateX = this.panStartTranslateX + clientX - this.panStartX;
    this.translateY = this.panStartTranslateY + clientY - this.panStartY;
    this.updateTransform();
  }

  /**
   * Updates the transform of the image.
   * Translates and scales based on the current zoom and pan values,
   * and sets the cursor based on zoom/pan state.
   */
  private updateTransform(): void {
    this.transform = `translate3d(${this.translateX}px, ${this.translateY}px, 0) scale(${this.scale})`;
    this.cursor = this.isZoomedIn() ? (this.isPanning ? 'grabbing' : 'grab') : 'default';

    // Apply immediately instead of waiting for next repaint
    this.element.nativeElement.style.transform = this.transform;
    this.element.nativeElement.style.cursor = this.cursor;
    
    this.updateOverflowState();
  }

  /**
   * Updates the overflow state of the image.
   * Applies overflow: hidden to the reader and reading area to prevent
   * scrollbars from appearing when zooming in.
   */
  private updateOverflowState(): void {
    if (!this.lockScroll) return;

    // Prevent unnecessary DOM manipulations
    const wasZoomedIn = this.element.nativeElement.dataset['imagezoomedin'] === 'true';
    if (wasZoomedIn === this.isZoomedIn()) { return; }
    this.element.nativeElement.dataset['imagezoomedin'] = this.isZoomedIn().toString();

    const readingArea = this.element.nativeElement.closest('.reading-area');
    const reader = readingArea?.closest('.reader');
    readingArea?.classList.toggle('image-zoom-active', this.isZoomedIn());
    reader?.classList.toggle('image-zoom-active', this.isZoomedIn());
  }

  /**
   * Gets the distance between two touches.
   * @param first The first touch
   * @param second The second touch
   * @returns The distance between the two touches
   */
  private getDistance(first: Touch, second: Touch): number {
    const deltaX = second.clientX - first.clientX;
    const deltaY = second.clientY - first.clientY;
    return Math.hypot(deltaX, deltaY);
  }

  /**
   * Gets the midpoint between two touches.
   * @param first The first touch
   * @param second The second touch
   * @returns The point between the two touches
   */
  private getMidpoint(first: Touch, second: Touch): { x: number; y: number } {
    return {
      x: (first.clientX + second.clientX) / 2,
      y: (first.clientY + second.clientY) / 2,
    };
  }

  constructor() {
    // Add this instance to the set of active image zoom instances
    ImageZoomDirective.instances.add(this);

    // Add document-level event listeners for when a zoom or pan event is triggered on
    // a pagination area and should be passed through to the underlying image
    const document = this.element.nativeElement.ownerDocument;
    if (!ImageZoomDirective.listenerDocument) {
      ImageZoomDirective.listenerDocument = document;
      document.addEventListener('wheel', ImageZoomDirective.wheelListener, {capture: true, passive: false});
      document.addEventListener('touchstart', ImageZoomDirective.touchStartListener, {capture: true, passive: false});
      document.addEventListener('touchmove', ImageZoomDirective.touchMoveListener, {capture: true, passive: false});
      document.addEventListener('touchend', ImageZoomDirective.touchEndListener, {capture: true});
    };

    /**
     * Prevents click events from being handled by the pagination areas
     * when the click is being handled as a pan.
     * @param event The mouse event
     */
    const suppressClick = (event: MouseEvent) => {
      if (!this.suppressNextPaginationClick || !this.isPaginationEvent(event)) {
        return;
      }

      this.suppressNextPaginationClick = false;
      event.preventDefault();
      event.stopPropagation();
    };
    document.addEventListener('click', suppressClick, {capture: true});

    inject(DestroyRef).onDestroy(() => {
      // Remove this instance from the set of active image zoom instances
      ImageZoomDirective.instances.delete(this);
      if (ImageZoomDirective.activeInstance === this) {
        ImageZoomDirective.activeInstance = undefined;
      }

      this.scale = 1;
      this.updateOverflowState();

      // Remove document-level event listeners if this was the last instance
      if (ImageZoomDirective.instances.size === 0 && ImageZoomDirective.listenerDocument) {
        const listenerDocument = ImageZoomDirective.listenerDocument;
        listenerDocument.removeEventListener('wheel', ImageZoomDirective.wheelListener, {capture: true});
        listenerDocument.removeEventListener('touchstart', ImageZoomDirective.touchStartListener, {capture: true});
        listenerDocument.removeEventListener('touchmove', ImageZoomDirective.touchMoveListener, {capture: true});
        listenerDocument.removeEventListener('touchend', ImageZoomDirective.touchEndListener, {capture: true});
        ImageZoomDirective.listenerDocument = undefined;
      }

      document.removeEventListener('click', suppressClick, {capture: true});
    });
  }

  /**
   * Gets the image zoom instance for a given event.
   * @param event The event for which to get the instance
   * @returns The image zoom instance or undefined if not found
   */
  private static getInstanceForEvent(event: WheelEvent | TouchEvent): ImageZoomDirective | undefined {
    const target = event.target instanceof Element ? event.target : null;
    
    if (target && [...ImageZoomDirective.instances].some(instance => instance.element.nativeElement.contains(target))) {
      return undefined;
    }

    const readingArea = target?.closest('.reading-area');
    if (!readingArea) {
      return undefined;
    }

    const activeInstance = ImageZoomDirective.activeInstance;
    if (activeInstance?.element.nativeElement.closest('.reading-area') === readingArea &&
        activeInstance.element.nativeElement.getBoundingClientRect().width > 0) {
      return activeInstance;
    }

    return undefined;
  }
}
