import {Component, contentChild, input, model, Pipe, PipeTransform, TemplateRef, viewChild} from '@angular/core';
import {FormValueControl} from "@angular/forms/signals";
import {ReactiveFormsModule} from "@angular/forms";
import {NgTemplateOutlet} from "@angular/common";

export interface EnumOption<T> {
  value: T;
  label?: string;
  title?: string;
}

@Component({
  imports: [
    ReactiveFormsModule,
    NgTemplateOutlet
  ],
  selector: 'app-setting-enum-select',
  styleUrl: './setting-enum-select.component.scss',
  templateUrl: './setting-enum-select.component.html',
})
export class SettingEnumSelectComponent<T extends number = number> implements FormValueControl<T> {

  value = model<T>(0 as T);
  disabled = input<boolean>(false);

  options = input.required<EnumOption<T>[]>();
  template = contentChild<TemplateRef<never>>('template');

  onSelectionChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    const numericValue = select.value === '' ? null : Number(select.value);
    this.value.set(numericValue as T);
  }

}
