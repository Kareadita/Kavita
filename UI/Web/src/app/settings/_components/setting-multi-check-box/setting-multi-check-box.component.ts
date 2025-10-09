import {
  ChangeDetectionStrategy,
  Component,
  computed, effect,
  forwardRef,
  input, model,
  signal
} from '@angular/core';
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {NgStyle} from "@angular/common";

/**
 * An item to display in the SettingMultiCheckBox
 */
export interface MultiCheckBoxItem<T> {
  /**
   * Label to display in the list
   */
  label: string,
  /**
   * Value passed to the FormControl
   */
  value: T,
  /**
   * Appends a dot after the label
   */
  colour?: RgbaColor,
  /**
   * If the items checkbox should be disabled. Does not overwrite global disable
   * @param value
   * @param selected
   */
  disableFunc?: (value: T, selected: T[]) => boolean,
}

/**
 * The SettingMultiCheckBox should be used when wanting to display all options, of which any may be selected at once.
 * The component should have a formControlName bound to it of type FormControl<T[]>.
 *
 * An example can be found in ManageUserPreferencesComponent
 */
@Component({
  selector: 'app-setting-multi-check-box',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ReactiveFormsModule,
    NgStyle
  ],
  standalone: true,
  templateUrl: './setting-multi-check-box.component.html',
  styleUrl: './setting-multi-check-box.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SettingMultiCheckBox<any>),
      multi: true,
    }
  ]
})
export class SettingMultiCheckBox<T> implements ControlValueAccessor {

  /**
   * Title to display above the checkboxes
   */
  title = input.required<string>();
  /**
   * Tooltip to display muted underneath the title
   * @optional
   */
  tooltip = input<string>('');
  /**
   * Loading indicator for the checkbox list
   * @optional
   */
  loading = input<boolean | undefined>(undefined);
  /**
   * All possible options
   */
  options = input.required<MultiCheckBoxItem<T>[]>();
  /**
   * Disable all checkboxes
   */
  disabled = model(false);

  isLoading = computed(() => {
    const loading = this.loading();
    return loading !== undefined && loading;
  });
  allSelected = computed(() => this.options().length === this.selectedValues().length);

  selectedValues = signal<T[]>([]);

  private _onChange: (value: T[]) => void = () => {};
  private _onTouched: () => void = () => {};

  constructor() {
    // Auto propagate changes to the FormGroup
    effect(() => {
      const selectedValues = this.selectedValues();
      this._onChange(selectedValues);
      this._onTouched();
    });
  }

  writeValue(obj: T[]): void {
    this.selectedValues.set(obj || []);
  }

  registerOnChange(fn: (_: T[]) => void): void {
    this._onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this._onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  isChecked(item: MultiCheckBoxItem<T>) {
    return this.selectedValues().includes(item.value);
  }

  isDisabled(item: MultiCheckBoxItem<T>) {
    const disabled = this.disabled();
    const selected = this.selectedValues();

    if (disabled) {
      return true;
    }

    return item.disableFunc && item.disableFunc(item.value, selected);
  }

  onCheckboxChange(item: MultiCheckBoxItem<T>, event: Event) {
    const checked = (event.target as HTMLInputElement).checked;

    if (checked) {
      this.selectedValues.update(x => [...x, item.value]);
    } else {
      this.selectedValues.update(x => x.filter(t => t !== item.value));
    }
  }

  toggleAll() {
    if (this.allSelected()) {
      this.selectedValues.set([]);
    } else {
      this.selectedValues.set(this.options().map(opt => opt.value));
    }
  }

}
