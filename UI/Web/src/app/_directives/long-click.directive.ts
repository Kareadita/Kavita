import {Directive, ElementRef, inject, input, OnDestroy, output} from '@angular/core';
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

  readonly longClick = output();

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
        tap(() => this.longClick.emit(undefined))
      ).subscribe();
  }

  ngOnDestroy(): void {
    if (this.eventSubscribe) {
      this.eventSubscribe.unsubscribe();
    }
  }

}
