import {computed, Directive, input, linkedSignal} from '@angular/core';

@Directive({
  selector: '[appBlurToggle]',
  standalone: true,
  host: {
    '[class.blur-text]': 'isBlurred()',
    '[style.cursor]': 'enabled() ? "pointer" : null',
    '[attr.role]': 'enabled() ? "button" : null',
    '[attr.tabindex]': 'enabled() ? "0" : null',
    '(click)': 'toggleBlur()',
    '(keydown.enter)': 'toggleBlur()',
    '(keydown.space)': 'toggleBlur($event)',
  }
})
export class BlurToggleDirective {
  readonly shouldBlur = input.required<boolean>({ alias: 'appBlurToggle' });
  readonly enabled = input(true, { alias: 'appBlurToggleEnabled' });
  readonly isBlurred = linkedSignal(() => this.shouldBlur());

  toggleBlur(event?: Event) {
    if (!this.enabled()) return;
    event?.preventDefault();
    this.isBlurred.update(x => !x);
  }
}
