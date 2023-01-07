import { Directive, ElementRef, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { fromEvent, map, Observable } from 'rxjs';

export enum SwipeDirection {
  /**
   * Only detects up/down events and fires those
   */
  UpDown = 1,
  /**
   * Only detects left to right events and fires those
   */
  RightLeft = 2,
  /**
   * Wont do any swipe work
   */
  Disabled = 3
}

/**
 * Repsonsible for triggering a swipe event 
 */
@Directive({
  selector: '[appSwipe]'
})
export class SwipeDirective {

  @Input() direction: SwipeDirection = SwipeDirection.RightLeft;
  @Output() swipeEvent: EventEmitter<any> = new EventEmitter<any>();
  threshold: number = 10;

  touchStarts$!: Observable<void>;
  touchMoves$!: Observable<void>;
  touchEnds$!: Observable<void>;
  touchCancels$!: Observable<TouchEvent>;

  @HostListener('touchstart') onTouchStart(event: TouchEvent) {
    console.log('Touch Start: ', event);
  }

  @HostListener('touchend') onTouchEnd(event: TouchEvent) {
    console.log('Touch End: ', event);
  }

  @HostListener('touchmove') onTouchMove(event: TouchEvent) {
    console.log('Touch Move: ', event);
  }

  @HostListener('touchcancel') onTouchCancel(event: TouchEvent) {
    console.log('Touch Cancel: ', event);
  }

  constructor(private el: ElementRef) {
    this.touchStarts$ = fromEvent<TouchEvent>(el.nativeElement, 'touchstart').pipe(map(this.getTouchCoordinates));
    this.touchMoves$ = fromEvent<TouchEvent>(el.nativeElement, 'touchmove').pipe(map(this.getTouchCoordinates));
    this.touchEnds$ = fromEvent<TouchEvent>(el.nativeElement, 'touchend').pipe(map(this.getTouchCoordinates));
    this.touchCancels$ = fromEvent<TouchEvent>(el.nativeElement, 'touchcancel');

    this.touchCancels$.subscribe();
    this.touchStarts$.subscribe();
    this.touchMoves$.subscribe();
    this.touchEnds$.subscribe();
    
  }

  getTouchCoordinates(event: TouchEvent) {
    console.log('event: ', event);
  }

}
