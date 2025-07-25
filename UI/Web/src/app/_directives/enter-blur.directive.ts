import { Directive, HostListener } from '@angular/core';

@Directive({
  selector: '[appEnterBlur]',
  standalone: true,
})
export class EnterBlurDirective {
  @HostListener('keydown.enter', ['$event'])
  onEnter(event: KeyboardEvent): void {
    event.preventDefault();
    document.body.click();
  }
}
