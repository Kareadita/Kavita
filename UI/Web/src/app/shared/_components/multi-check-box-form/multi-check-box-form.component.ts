import {
  ChangeDetectionStrategy,
  Component,
  computed, effect,
  forwardRef,
  input,
  signal
} from '@angular/core';
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../loading/loading.component";
import {NgStyle} from "@angular/common";

/**
 * An item to display in the MultiCheckBoxFormComponent
 */
interface MultiSelectCheckBoxFormItem<T> {
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
}

/**
 * The MultiCheckBoxFormComponent should be used when wanting to display all options, of which any may be selected at once.
 * The component should have a formControlName bound to it of type FormControl<T[]>.
 *
 * An example can be found in ManageUserPreferencesComponent
 */
@Component({
  selector: 'app-multi-check-box-form',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ReactiveFormsModule,
    NgStyle
  ],
  standalone: true,
  templateUrl: './multi-check-box-form.component.html',
  styleUrl: './multi-check-box-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MultiCheckBoxFormComponent<any>),
      multi: true,
    }
  ]
})
export class MultiCheckBoxFormComponent<T> implements ControlValueAccessor {

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
  options = input.required<MultiSelectCheckBoxFormItem<T>[]>();

  isLoading = computed(() => {
    const loading = this.loading();
    return loading !== undefined && loading;
  });
  allSelected = computed(() => this.options().length === this.selectedValues().length);

  selectedValues = signal<T[]>([]);
  disabled = signal(false);

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

  isChecked(item: MultiSelectCheckBoxFormItem<T>) {
    return this.selectedValues().includes(item.value);
  }

  onCheckboxChange(item: MultiSelectCheckBoxFormItem<T>, event: Event) {
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
