import { Directive, ElementRef, Input, OnInit } from '@angular/core';
import { BehaviorSubject, fromEvent, Observable, timer, takeUntil } from 'rxjs';

/**
 * When a double click occurs, the event passed will be invoked
 */
@Directive({
  selector: '[dblClick]'
})
export class DblClickDirective  implements OnInit {

  @Input('dblClick') actionFn!: (() => any) | (() => void);

  private click$!: Observable<unknown>;
  private clickTimeout: number = 500;
  private timer$!: Observable<number>;
  private isConfirming = new BehaviorSubject<boolean>(false);

  constructor(private el: ElementRef) { 
    if (el.nativeElement) {
      this.click$ = fromEvent(this.el.nativeElement, 'click');
    }
  }

  ngOnInit(): void {
    if (this.click$) {
      this.click$.subscribe((event: any) => this.handleDoubleClick());
    }
  }

  handleDoubleClick() {
    if (this.isConfirming.value === false) {
      // start confirming
      this.timer$ = timer(this.clickTimeout);
      this.isConfirming.next(true);

      // start the timer
      this.timer$
        .pipe(
          takeUntil(this.click$) // stop timer when confirm$ emits (this conveniently happens when the button is clicked again)
        )
        .subscribe(() => {
          this.isConfirming.next(false); // timeout done - confirm cancelled
        });
    } else {
      // delete confirmation
      this.isConfirming.next(false);
      if (this.actionFn !== undefined) {
        this.actionFn();
      }
    }
  }

}
