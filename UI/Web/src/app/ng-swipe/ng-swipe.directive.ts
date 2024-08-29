import {Directive, ElementRef, EventEmitter, Input, NgZone, OnDestroy, OnInit, Output} from '@angular/core';
import { Subscription } from 'rxjs';
import {createSwipeSubscription, SwipeDirection, SwipeEvent, SwipeStartEvent} from './ag-swipe.core';

@Directive({
    selector: '[ngSwipe]',
    standalone: true
})
export class SwipeDirective implements OnInit, OnDestroy {
  private swipeSubscription: Subscription | undefined;

  @Input() restrictSwipeToLeftSide: boolean = false;
  @Output() swipeMove: EventEmitter<SwipeEvent> = new EventEmitter<SwipeEvent>();
  @Output() swipeEnd: EventEmitter<SwipeEvent> = new EventEmitter<SwipeEvent>();
  @Output() swipeLeft: EventEmitter<void> = new EventEmitter<void>();
  @Output() swipeRight: EventEmitter<void> = new EventEmitter<void>();
  @Output() swipeUp: EventEmitter<void> = new EventEmitter<void>();
  @Output() swipeDown: EventEmitter<void> = new EventEmitter<void>();

  constructor(
    private elementRef: ElementRef,
    private zone: NgZone
  ) {}

  ngOnInit() {
    this.zone.runOutsideAngular(() => {
      this.swipeSubscription = createSwipeSubscription({
        domElement: this.elementRef.nativeElement,
        onSwipeMove: (swipeMoveEvent: SwipeEvent) => this.swipeMove.emit(swipeMoveEvent),
        onSwipeEnd: (swipeEndEvent: SwipeEvent) => {
          if (this.isSwipeWithinRestrictedArea(swipeEndEvent)) {
            this.swipeEnd.emit(swipeEndEvent);
            this.detectSwipeDirection(swipeEndEvent);
          }
        }
      });
    });
  }

  private isSwipeWithinRestrictedArea(swipeEvent: SwipeEvent): boolean {
    if (!this.restrictSwipeToLeftSide) return true; // If restriction is disabled, allow all swipes

    const elementRect = this.elementRef.nativeElement.getBoundingClientRect();
    const touchAreaWidth = elementRect.width * 0.3; // Define the left area (30% of the element's width)

    // Assuming swipeEvent includes the starting coordinates; you may need to adjust this logic
    if (swipeEvent.direction === SwipeDirection.X && Math.abs(swipeEvent.distance) < touchAreaWidth) {
      return true;
    }

    return false;
  }

  private detectSwipeDirection(swipeEvent: SwipeEvent) {
    if (swipeEvent.direction === SwipeDirection.X) {
      if (swipeEvent.distance > 0) {
        this.swipeRight.emit();
      } else {
        this.swipeLeft.emit();
      }
    } else if (swipeEvent.direction === SwipeDirection.Y) {
      if (swipeEvent.distance > 0) {
        this.swipeDown.emit();
      } else {
        this.swipeUp.emit();
      }
    }
  }



  ngOnDestroy() {
    this.swipeSubscription?.unsubscribe();
  }
}
