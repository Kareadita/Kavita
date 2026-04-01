import {Directive, input, linkedSignal} from '@angular/core';

@Directive({
  selector: '[appBlurToggle]',
  standalone: true,
  host: {
    '[class.blur-text]': 'isBlurred()',
    '[style.cursor]': '"pointer"',
    'role': 'button',
    'tabindex': '0',
    '(click)': 'toggleBlur()',
    '(keydown.enter)': 'toggleBlur()',
    '(keydown.space)': 'toggleBlur($event)',
  }
})
export class BlurToggleDirective {
  readonly shouldBlur = input.required<boolean>({ alias: 'appBlurToggle' });
  readonly isBlurred = linkedSignal(() => this.shouldBlur());

  toggleBlur(event?: Event) {
    event?.preventDefault();
    this.isBlurred.update(x => !x);
  }
}
