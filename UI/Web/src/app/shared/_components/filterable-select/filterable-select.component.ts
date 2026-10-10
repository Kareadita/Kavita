import {ChangeDetectionStrategy, Component, computed, input, model} from '@angular/core';
import {FormValueControl} from "@angular/forms/signals";
import {Select2, Select2Option, Select2UpdateValue} from "ng-select2-component";
import {EnumOption} from "../../../settings/_components/setting-enum-select/setting-select.component";

/**
 * A searchable single select bound with [formField]. Wraps ng-select2-component, keep select2 types out of the inputs
 * so it can be replaced here without touching callers
 */
@Component({
  selector: 'app-filterable-select',
  imports: [Select2],
  templateUrl: './filterable-select.component.html',
  styleUrl: './filterable-select.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterableSelectComponent<T extends number | string = number> implements FormValueControl<T | null> {

  readonly inputId = input.required<string>();
  /** Visually hidden, read by screen readers */
  readonly label = input.required<string>();
  /** Shown while nothing is selected; clearing the selection sets `null` */
  readonly placeholder = input<string | undefined>(undefined);
  readonly options = input.required<EnumOption<T>[]>();
  value = model<T | null>(null);

  protected readonly data = computed<Select2Option[]>(() => this.options()
    .map(opt => ({value: opt.value, label: opt.title ?? opt.label ?? String(opt.value)})));

  protected updateValue(next: Select2UpdateValue) {
    // select2 reports undefined for a value with no matching option, and echoes the current value back
    const value = (next ?? null) as T | null;
    if (value === this.value()) return;

    this.value.set(value);
  }
}
