import {ChangeDetectionStrategy, Component, effect, forwardRef, inject, signal} from '@angular/core';
import {ControlValueAccessor, NG_VALUE_ACCESSOR} from "@angular/forms";
import {KeyBindService, KeyCode, KeyCombo, ModifierKeyCodes} from "../../../_services/key-bind.service";

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

  protected readonly keyBindService = inject(KeyBindService);

  selectedKeys = signal<KeyCombo>([]);
  disabled = signal(false);

  private _onChange: (value: KeyCombo) => void = () => {};
  private _onTouched: () => void = () => {};

  constructor() {
    effect(() => {
      const selectedKeys = this.selectedKeys();
      this._onChange(selectedKeys);
      this._onTouched();
    });
  }

  writeValue(keys: KeyCode[]): void {
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
    const keys = new Set<KeyCode>();
    if (event.ctrlKey) keys.add(KeyCode.Control);
    if (event.altKey) keys.add(KeyCode.Alt);
    if (event.shiftKey) keys.add(KeyCode.Shift);
    if (event.metaKey) keys.add(KeyCode.Meta);

    if (!ModifierKeyCodes.includes(event.code as KeyCode)) {
      keys.add(event.code as KeyCode)
    }

    this.selectedKeys.set([...keys]);

    event.preventDefault();
    event.stopPropagation();
  };
}
