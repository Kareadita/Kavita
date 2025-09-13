import {Directive, EventEmitter, HostListener, Output} from '@angular/core';

@Directive({
  selector: '[appDblClick]',
  standalone: true
})
export class DblClickDirective {

  @Output() singleClick = new EventEmitter<Event>();
  @Output() doubleClick = new EventEmitter<Event>();

  private lastTapTime = 0;
  private tapTimeout = 300; // Time threshold for a double tap (in milliseconds)
  private singleClickTimeout: any;

  @HostListener('click', ['$event'])
  handleClick(event: Event): void {
    const currentTime = new Date().getTime();

    if (currentTime - this.lastTapTime < this.tapTimeout) {
      // Detected a double click/tap
      clearTimeout(this.singleClickTimeout); // Prevent single-click emission
      event.stopPropagation();
      event.preventDefault();
      this.doubleClick.emit(event);
    } else {
      // Delay single-click emission to check if a double-click occurs
      this.singleClickTimeout = setTimeout(() => {
        this.singleClick.emit(event); // Optional: emit single-click if no double-click follows
      }, this.tapTimeout);
    }

    this.lastTapTime = currentTime;
  }

}
