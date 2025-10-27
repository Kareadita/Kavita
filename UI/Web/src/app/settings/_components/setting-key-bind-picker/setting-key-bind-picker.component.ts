import {ChangeDetectionStrategy, Component, effect, forwardRef, signal} from '@angular/core';
import {ControlValueAccessor, NG_VALUE_ACCESSOR} from "@angular/forms";
import {KEY_CODES} from "../../../shared/_services/utility.service";
import {getReadableComboLabel, KeyCode, ModifierKeyCodes} from "../../../_services/key-bind.service";

@Component({
  selector: 'app-setting-key-bind-picker',
  imports: [],
  templateUrl: './setting-key-bind-picker.component.html',
  styleUrl: './setting-key-bind-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SettingKeyBindPickerComponent),
      multi: true,
    }
  ]
})
export class SettingKeyBindPickerComponent implements ControlValueAccessor {

  selectedKeys = signal<string[]>([]);
  disabled = signal(false);

  private _onChange: (value: string[]) => void = () => {};
  private _onTouched: () => void = () => {};

  constructor() {
    effect(() => {
      const selectedKeys = this.selectedKeys();
      this._onChange(selectedKeys);
      this._onTouched();
    });
  }

  writeValue(keys: string[]): void {
      this.selectedKeys.set(keys)
  }

  registerOnChange(fn: (_: string[]) => void): void {
    this._onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this._onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  startListening() {
    window.addEventListener('keydown', this.onKeyDown);
  }

  stopListening() {
    window.removeEventListener('keydown', this.onKeyDown);
  }

  private onKeyDown = (event: KeyboardEvent) => {
    const keys = new Set<string>();
    if (event.ctrlKey) keys.add(KEY_CODES.CONTROL);
    if (event.altKey) keys.add(KEY_CODES.ALT);
    if (event.shiftKey) keys.add(KEY_CODES.SHIFT);
    if (event.metaKey) keys.add(KEY_CODES.META);

    if (!ModifierKeyCodes.includes(event.code as KeyCode)) {
      keys.add(event.code)
    }

    this.selectedKeys.set([...keys]);

    event.preventDefault();
    event.stopPropagation();
  };

  protected readonly getReadableComboLabel = getReadableComboLabel;
}
