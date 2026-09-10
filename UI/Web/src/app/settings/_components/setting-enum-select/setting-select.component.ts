import {Component, contentChild, inject, input, model, TemplateRef} from '@angular/core';
import {FormField, FormValueControl} from "@angular/forms/signals";
import {NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

export interface EnumOption<T> {
  value: T;
  label?: string;
  title?: string;
}

/**
 * A wrapper around a native select element that supports emitting non-string (enum) values.
 */
@Component({
  imports: [
    NgTemplateOutlet,
    TitleCasePipe,
    FormFieldDirective
  ],
  selector: 'app-setting-select',
  styleUrl: './setting-select.component.scss',
  templateUrl: './setting-select.component.html',
})
export class SettingSelectComponent<T extends number = number> implements FormValueControl<T> {

  protected readonly formField = inject(FormField);

  /** This should only be passed when used outside an <app-setting-item> */
  inputId = input<string | undefined>(undefined);
  value = model<T>(0 as T);
  extraClasses = input<string>('');

  options = input.required<(EnumOption<T> | T)[]>();
  template = contentChild<TemplateRef<never>>('template');

  getEnumValue(opt: T | EnumOption<T>): T {
    return typeof opt === 'number' ? opt : opt.value;
  }

  getEnumLabel(opt: T | EnumOption<T>): string {
    if (typeof opt === 'number') {
      return opt + '';
    }

    return opt.title ?? opt.label ?? (opt.value + '');
  }

  onSelectionChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    const numericValue = select.value === '' ? null : Number(select.value);
    this.value.set(numericValue as T);
  }
}
