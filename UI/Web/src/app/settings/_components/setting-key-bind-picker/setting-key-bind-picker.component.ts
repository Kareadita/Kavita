import {ChangeDetectionStrategy, Component, effect, forwardRef, inject, signal} from '@angular/core';
import {ControlValueAccessor, NG_VALUE_ACCESSOR} from "@angular/forms";
import {KeyBindService, KeyCode, ModifierKeyCodes} from "../../../_services/key-bind.service";
import {KeyBind} from "../../../_models/preferences/preferences";
import {KeyBindPipe} from "../../../_pipes/key-bind.pipe";

@Component({
  selector: 'app-setting-key-bind-picker',
  imports: [
    KeyBindPipe
  ],
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

  selectedKeyBind = signal<KeyBind>({key: KeyCode.Empty});
  disabled = signal(false);

  private _onChange: (value: KeyBind) => void = () => {};
  private _onTouched: () => void = () => {};

  constructor() {
    effect(() => {
      const selectedKeys = this.selectedKeyBind();
      this._onChange(selectedKeys);
      this._onTouched();
    });
  }

  writeValue(keyBind: KeyBind): void {
      this.selectedKeyBind.set(keyBind)
  }

  registerOnChange(fn: (_: KeyBind) => void): void {
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
    const eventKey = event.key.toLowerCase() as KeyCode;

    this.selectedKeyBind.set({
      key: ModifierKeyCodes.includes(eventKey) ? KeyCode.Empty : eventKey,
      meta: event.metaKey,
      alt: event.altKey,
      control: event.ctrlKey,
      shift: event.shiftKey,
    });

    event.preventDefault();
    event.stopPropagation();
  };
}
