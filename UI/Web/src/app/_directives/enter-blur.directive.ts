import {Directive, HostListener} from '@angular/core';

/**
 * When enter is pressed, the focus is lost
 */
@Directive({
  selector: '[appEnterBlur]',
  standalone: true,
})
export class EnterBlurDirective {
  @HostListener('keydown.enter', ['$event'])
  onEnter(event: Event): void {
    event.preventDefault();
    document.body.click();
  }
}
