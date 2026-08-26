import {
  AfterContentInit,
  ChangeDetectionStrategy,
  Component,
  contentChild,
  ElementRef,
  inject,
  input,
  signal,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {generateUniqueId} from "../../../_helpers/random";

@Component({
    selector: 'app-setting-switch',
    imports: [
        NgTemplateOutlet,
        TranslocoDirective,
        SafeHtmlPipe
    ],
    templateUrl: './setting-switch.component.html',
    styleUrl: './setting-switch.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingSwitchComponent implements AfterContentInit {

  private readonly elementRef = inject(ElementRef);

  title = input.required<string>();
  subtitle = input<string | undefined>();
  id = input<string | undefined>();
  /** For wiring up with a real label */
  labelId = signal('');
  switchRef = contentChild<TemplateRef<any>>('switch');

  ngAfterContentInit(): void {
    setTimeout(() => {
      if (this.id()) {
        this.labelId.set(this.id()!);
        return;
      }

      const element = this.elementRef.nativeElement;
      const inputElement = element.querySelector('input');

      // If no id, generate a random id and assign it to the input
      inputElement.id = generateUniqueId();

      if (inputElement && inputElement.id) {
        this.labelId.set(inputElement.id);
      } else {
        console.warn('No input with ID found in app-setting-switch. For accessibility, please ensure the input has an ID.');
      }
    });
  }
}
