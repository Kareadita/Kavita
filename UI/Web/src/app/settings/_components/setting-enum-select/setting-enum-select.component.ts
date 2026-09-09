import {
  Component,
  computed,
  contentChild, inject,
  input,
  model,
  Pipe,
  PipeTransform,
  TemplateRef,
  viewChild
} from '@angular/core';
import {FormField, FormValueControl} from "@angular/forms/signals";
import {ReactiveFormsModule} from "@angular/forms";
import {NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

export interface EnumOption<T> {
  value: T;
  label?: string;
  title?: string;
}

@Component({
  imports: [
    ReactiveFormsModule,
    NgTemplateOutlet,
    TitleCasePipe,
    FormFieldDirective
  ],
  selector: 'app-setting-enum-select',
  styleUrl: './setting-enum-select.component.scss',
  templateUrl: './setting-enum-select.component.html',
})
export class SettingEnumSelectComponent<T extends number = number> implements FormValueControl<T> {

  protected formField = inject(FormField);

  id = input.required<string>();

  value = model<T>(0 as T);
  disabled = input<boolean>(false);
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
