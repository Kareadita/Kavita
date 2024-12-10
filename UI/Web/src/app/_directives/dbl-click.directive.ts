import {Directive, EventEmitter, HostListener, Output} from '@angular/core';

@Directive({
  selector: '[appDblClick]',
  standalone: true
})
export class DblClickDirective {

  @Output() doubleClick = new EventEmitter<Event>();

  private lastTapTime = 0;
  private tapTimeout = 300; // Time threshold for a double tap (in milliseconds)

  @HostListener('click', ['$event'])
  handleClick(event: Event): void {
    event.stopPropagation();
    event.preventDefault();

    const currentTime = new Date().getTime();
    if (currentTime - this.lastTapTime < this.tapTimeout) {
      // Detected a double click/tap
      this.doubleClick.emit(event);
    }
    this.lastTapTime = currentTime;
  }

}
