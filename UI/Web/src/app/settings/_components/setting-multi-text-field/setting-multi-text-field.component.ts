import {ChangeDetectionStrategy, Component, computed, effect, forwardRef, input, signal} from '@angular/core';
import {ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR, ReactiveFormsModule} from "@angular/forms";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {SettingItemComponent} from "../setting-item/setting-item.component";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";

/**
 * SettingMultiTextFieldComponent should be used when using a text area to input several comma seperated values.
 * The component should have a formControlName bound to it of type FormControl<T[]>.
 * By default, T is assumed to be a string
 *
 * An example can be found in ManageOpenIDConnectComponent
 */
@Component({
  selector: 'app-setting-multi-text-field',
  imports: [
    DefaultValuePipe,
    FormsModule,
    ReactiveFormsModule,
    SettingItemComponent,
    TagBadgeComponent
  ],
  templateUrl: './setting-multi-text-field.component.html',
  styleUrl: './setting-multi-text-field.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SettingMultiTextFieldComponent),
      multi: true,
    }
  ]
})
export class SettingMultiTextFieldComponent<T> implements ControlValueAccessor {
  /**
   * Convertor, required if your type is not a string
   * @default trimmed string value
   */
  valueConvertor = input<(s: string) => T>((t: string) => t.trim() as T);
  /**
   * String to value convertor, required if your type is not a string
   * @default the value as string
   */
  stringConvertor = input<(t: T) => string>((t: T) => (t as string));
  /**
   * Filter, required if your type is not a string
   * @default non empty strings
   */
  valueFilter = input<(t: T) => boolean>((t: T) => (t as string).length > 0);
  /**
   * Title to display
   */
  title = input.required<string>();
  /**
   * Tooltip to display
   * @optional
   */
  tooltip = input<string>('');
  /**
   * Loading indicator for the checkbox list
   * @optional
   */
  loading = input<boolean | undefined>(undefined);
  /**
   * id for the textarea input
   * @optional
   */
  id = input<string>('');

  isLoading = computed(() => {
    const loading = this.loading();
    return loading !== undefined && loading;
  });
  textFieldValue = computed(() => this.selectedValues().map(this.stringConvertor()).join(','))
  selectedValues = signal<T[]>([]);
  disabled = signal(false);

  textFieldValueTracker = '';

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
    this.textFieldValueTracker = obj.map(this.stringConvertor()).join(',');
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

  onTextFieldChange(event: Event) {
    const input = (event.target as HTMLTextAreaElement).value;
    this.selectedValues.set(input
      .split(',')
      .map(this.valueConvertor())
      .filter(this.valueFilter())
    );
  }
}
