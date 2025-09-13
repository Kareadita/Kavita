import {Directive, ElementRef, EventEmitter, inject, input, OnDestroy, Output} from '@angular/core';
import {fromEvent, merge, Subscription, switchMap, tap, timer} from "rxjs";
import {takeUntil} from "rxjs/operators";

@Directive({
  selector: '[appLongClick]',
  standalone: true
})
export class LongClickDirective implements OnDestroy {

  private elementRef: ElementRef = inject(ElementRef);

  private readonly eventSubscribe: Subscription;

  /**
   * How long should the element be pressed for
   * @default 500
   */
  threshold = input(500);

  @Output() longClick = new EventEmitter();

  constructor() {
    const start$ = merge(
      fromEvent(this.elementRef.nativeElement, 'touchstart'),
      fromEvent(this.elementRef.nativeElement, 'mousedown')
    );

    const end$ = merge(
      fromEvent(this.elementRef.nativeElement, 'touchend'),
      fromEvent(this.elementRef.nativeElement, 'mouseup')
    );

    this.eventSubscribe = start$
      .pipe(
        switchMap(() => timer(this.threshold()).pipe(takeUntil(end$))),
        tap(() => this.longClick.emit())
      ).subscribe();
  }

  ngOnDestroy(): void {
    if (this.eventSubscribe) {
      this.eventSubscribe.unsubscribe();
    }
  }

}
