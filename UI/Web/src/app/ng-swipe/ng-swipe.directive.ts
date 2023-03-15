import { Directive, ElementRef, EventEmitter, NgZone, OnDestroy, OnInit, Output } from '@angular/core';
import { Subscription } from 'rxjs';
import { createSwipeSubscription, SwipeEvent } from 'ag-swipe-core';

@Directive({
  selector: '[ngSwipe]'
})
export class SwipeDirective implements OnInit, OnDestroy {
  private swipeSubscription: Subscription | undefined;

  @Output() swipeMove: EventEmitter<SwipeEvent> = new EventEmitter<SwipeEvent>();
  @Output() swipeEnd: EventEmitter<SwipeEvent> = new EventEmitter<SwipeEvent>();

  constructor(
    private elementRef: ElementRef,
    private zone: NgZone
  ) {}

  ngOnInit() {
    this.zone.runOutsideAngular(() => {
      this.swipeSubscription = createSwipeSubscription({
        domElement: this.elementRef.nativeElement,
        onSwipeMove: (swipeMoveEvent: SwipeEvent) => this.swipeMove.emit(swipeMoveEvent),
        onSwipeEnd: (swipeEndEvent: SwipeEvent) => this.swipeEnd.emit(swipeEndEvent)
      });
    });
  }

  ngOnDestroy() {
    this.swipeSubscription?.unsubscribe?.();
  }
}
