import { Directive, ElementRef, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { fromEvent, map, Observable } from 'rxjs';

/**
 * Repsonsible for triggering a swipe event 
 */
@Directive({
  selector: '[appSwipe]'
})
export class SwipeDirective {

  @Input() threshold: number = 10;
  @Output() swipeEvent: EventEmitter<any> = new EventEmitter<any>();

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

  constructor(private el: ElementRef) {
    this.touchStarts$ = fromEvent<TouchEvent>(el.nativeElement, 'touchstart').pipe(map(this.getTouchCoordinates));
    this.touchMoves$ = fromEvent<TouchEvent>(el.nativeElement, 'touchmove').pipe(map(this.getTouchCoordinates));
    this.touchEnds$ = fromEvent<TouchEvent>(el.nativeElement, 'touchend').pipe(map(this.getTouchCoordinates));
    this.touchCancels$ = fromEvent<TouchEvent>(el.nativeElement, 'touchcancel');
  }

  getTouchCoordinates(event: TouchEvent) {

  }

}
