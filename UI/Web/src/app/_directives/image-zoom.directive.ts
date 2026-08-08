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
  private static activeInstance: ImageZoomDirective | undefined;
  private static readonly instances = new Set<ImageZoomDirective>();
  private static listenerDocument: Document | undefined;
  private static readonly wheelListener = (event: WheelEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onWheel(event);
    }
  };
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
  private static readonly touchMoveListener = (event: TouchEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onTouchMove(event);
    }
  };
  private static readonly touchEndListener = (event: TouchEvent) => {
    const instance = ImageZoomDirective.getInstanceForEvent(event);
    if (instance) {
      instance.onTouchEnd(event);
    }
  };
  private suppressNextPaginationClick = false;
  private readonly element: ElementRef<HTMLElement> = inject(ElementRef<HTMLElement>);
  private scale = 1;
  private translateX = 0;
  private translateY = 0;
  private pinchDistance = 0;
  private panStartX = 0;
  private panStartY = 0;
  private panStartTranslateX = 0;
  private panStartTranslateY = 0;
  private isPanning = false;
  private panStartedInPagination = false;

  transform = 'translate3d(0, 0, 0) scale(1)';
  cursor = 'default';

  @Input()
  set zoomResetKey(_: unknown) {
    ImageZoomDirective.activeInstance = this;
    this.reset();
  }

  @Input() lockScroll = true;
  @Input() recenterOnZoomOut = true;

  onWheel(event: WheelEvent): void {
    if (!event.ctrlKey && !event.metaKey) {
      return;
    }

    event.preventDefault();
    ImageZoomDirective.activeInstance = this;
    
    if (this.isPanning) {
      return;
    }

    const delta = Math.exp(-event.deltaY * 0.0015);
    this.setScale(this.scale * delta, event.clientX, event.clientY);
  }

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

  onTouchMove(event: TouchEvent): void {
    // Zoom
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

    if (event.touches.length !== 1 || !this.isPanning || !this.isZoomedIn()) return;

    // Pan
    event.preventDefault();
    event.stopPropagation();
    this.setPan(event.touches[0].clientX, event.touches[0].clientY);
  }

  onTouchEnd(event: TouchEvent): void {
    if (event.touches.length < 2) {
      this.pinchDistance = 0;
    }
    if (event.touches.length === 0) {
      this.isPanning = false;
    }
  }

  onMouseDown(event: MouseEvent): void {
    if (!this.canStartPan(event.button, event.clientX, event.clientY)) {
      return;
    }

    event.preventDefault();
    this.startPan(event.clientX, event.clientY);
  }

  onWindowMouseDown(event: MouseEvent): void {
    if (!this.isPaginationEvent(event) || !this.canStartPan(event.button, event.clientX, event.clientY)) {
      return;
    }

    event.preventDefault();
    this.startPan(event.clientX, event.clientY, true);
  }

  onMouseMove(event: MouseEvent): void {
    if (!this.isPanning) {
      return;
    }

    event.preventDefault();
    this.setPan(event.clientX, event.clientY);
  }

  onWindowTouchStart(event: TouchEvent): void {
    if (event.touches.length !== 1 || !this.isPaginationEvent(event)) {
      return;
    }

    if (!this.isZoomedIn() || !this.containsPoint(event.touches[0].clientX, event.touches[0].clientY)) {
      return;
    }

    this.startPan(event.touches[0].clientX, event.touches[0].clientY, true);
  }

  onWindowTouchMove(event: TouchEvent): void {
    if (event.touches.length !== 1 || !this.isPanning || !this.isZoomedIn()) {
      return;
    }

    event.preventDefault();
    this.setPan(event.touches[0].clientX, event.touches[0].clientY);
  }

  onMouseUp(): void {
    this.isPanning = false;
    this.cursor = this.isZoomedIn() ? 'grab' : 'default';
  }

  private reset(): void {
    this.scale = 1;
    this.translateX = 0;
    this.translateY = 0;
    this.pinchDistance = 0;
    this.isPanning = false;
    this.updateTransform();
  }

  private canStartPan(button: number, clientX: number, clientY: number): boolean {
    return button === 0 && this.isZoomedIn() && this.containsPoint(clientX, clientY);
  }

  private isPaginationEvent(event: MouseEvent | TouchEvent): boolean {
    return event.target instanceof Element && !!event.target.closest('.pagination-area');
  }

  private isZoomedIn(): boolean {
    return this.scale > 1;
  }

  private containsPoint(clientX: number, clientY: number): boolean {
    const rect = this.element.nativeElement.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 &&
      clientX >= rect.left && clientX <= rect.right &&
      clientY >= rect.top && clientY <= rect.bottom;
  }

  private setScale(nextScale: number, clientX: number, clientY: number): void {
    const previousScale = this.scale;
    const nextScaleClamped = Math.max(1, Math.min(4, nextScale));
    const rect = this.element.nativeElement.getBoundingClientRect();
    const layoutCenterX = (rect.left + rect.right) / 2 - this.translateX;
    const layoutCenterY = (rect.top + rect.bottom) / 2 - this.translateY;
    const imagePointX = (clientX - layoutCenterX - this.translateX) / previousScale;
    const imagePointY = (clientY - layoutCenterY - this.translateY) / previousScale;

    if (nextScaleClamped === 1) {
      this.scale = 1;
      this.translateX = 0;
      this.translateY = 0;
      this.updateTransform();
      return;
    }

    this.scale = nextScaleClamped;
    const focalTranslateX = clientX - layoutCenterX - imagePointX * this.scale;
    const focalTranslateY = clientY - layoutCenterY - imagePointY * this.scale;
    const zoomOutRatio = this.recenterOnZoomOut && nextScaleClamped < previousScale
      ? Math.max(0, Math.min(1, (nextScaleClamped - 1) / (previousScale - 1)))
      : 1;
    this.translateX = focalTranslateX * zoomOutRatio;
    this.translateY = focalTranslateY * zoomOutRatio;
    this.updateTransform();
  }

  private startPan(clientX: number, clientY: number, startedInPagination = false): void {
    this.panStartX = clientX;
    this.panStartY = clientY;
    this.panStartTranslateX = this.translateX;
    this.panStartTranslateY = this.translateY;
    this.isPanning = true;
    this.panStartedInPagination = startedInPagination;
    this.cursor = 'grabbing';
  }

  private setPan(clientX: number, clientY: number): void {
    if (this.panStartedInPagination && (clientX !== this.panStartX || clientY !== this.panStartY)) {
      this.suppressNextPaginationClick = true;
    }
    this.translateX = this.panStartTranslateX + clientX - this.panStartX;
    this.translateY = this.panStartTranslateY + clientY - this.panStartY;
    this.updateTransform();
  }

  private updateTransform(): void {
    this.transform = `translate3d(${this.translateX}px, ${this.translateY}px, 0) scale(${this.scale})`;
    this.cursor = this.isZoomedIn() ? (this.isPanning ? 'grabbing' : 'grab') : 'default';
    // Apply immediately instead of waiting for next repaint
    this.element.nativeElement.style.transform = this.transform;
    this.element.nativeElement.style.cursor = this.cursor;
    this.updateOverflowState();
  }

  private updateOverflowState(): void {
    if (!this.lockScroll) return;

    let ancestor = this.element.nativeElement.parentElement;
    while (ancestor) {
      if (ancestor.matches('.reading-area, .reader')) {
        ancestor.classList.toggle('image-zoom-active', this.isZoomedIn());
      }
      ancestor = ancestor.parentElement;
    }
  }

  private getDistance(first: Touch, second: Touch): number {
    const deltaX = second.clientX - first.clientX;
    const deltaY = second.clientY - first.clientY;
    return Math.hypot(deltaX, deltaY);
  }

  private getMidpoint(first: Touch, second: Touch): { x: number; y: number } {
    return {
      x: (first.clientX + second.clientX) / 2,
      y: (first.clientY + second.clientY) / 2,
    };
  }

  constructor() {
    ImageZoomDirective.instances.add(this);
    const document = this.element.nativeElement.ownerDocument;
    if (!ImageZoomDirective.listenerDocument) {
      ImageZoomDirective.listenerDocument = document;
      document.addEventListener('wheel', ImageZoomDirective.wheelListener, {capture: true, passive: false});
      document.addEventListener('touchstart', ImageZoomDirective.touchStartListener, {capture: true, passive: false});
      document.addEventListener('touchmove', ImageZoomDirective.touchMoveListener, {capture: true, passive: false});
      document.addEventListener('touchend', ImageZoomDirective.touchEndListener, true);
    };

    const suppressClick = (event: MouseEvent) => {
      if (!this.suppressNextPaginationClick || !this.isPaginationEvent(event)) {
        return;
      }

      this.suppressNextPaginationClick = false;
      event.preventDefault();
      event.stopPropagation();
    };
    document.addEventListener('click', suppressClick, true);

    inject(DestroyRef).onDestroy(() => {
      ImageZoomDirective.instances.delete(this);
      if (ImageZoomDirective.activeInstance === this) {
        ImageZoomDirective.activeInstance = undefined;
      }
      this.scale = 1;
      this.updateOverflowState();
      if (ImageZoomDirective.instances.size === 0 && ImageZoomDirective.listenerDocument) {
        const listenerDocument = ImageZoomDirective.listenerDocument;
        listenerDocument.removeEventListener('wheel', ImageZoomDirective.wheelListener, {capture: true});
        listenerDocument.removeEventListener('touchstart', ImageZoomDirective.touchStartListener, {capture: true});
        listenerDocument.removeEventListener('touchmove', ImageZoomDirective.touchMoveListener, {capture: true});
        listenerDocument.removeEventListener('touchend', ImageZoomDirective.touchEndListener, true);
        ImageZoomDirective.listenerDocument = undefined;
      }

      document.removeEventListener('click', suppressClick, true);
    });
  }

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
