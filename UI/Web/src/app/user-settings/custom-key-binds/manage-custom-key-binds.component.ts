import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {
  DefaultKeyBinds,
  getReadableComboLabel,
  KeyBindService, KeyCode, ModifierKeyCodes
} from "../../_services/key-bind.service";
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule, ValidationErrors, ValidatorFn
} from "@angular/forms";
import {KeyBindTarget} from "../../_models/preferences/preferences";
import {TranslocoService} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  SettingKeyBindPickerComponent
} from "../../settings/_components/setting-key-bind-picker/setting-key-bind-picker.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, filter, switchMap, tap} from "rxjs";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {TagBadgeComponent, TagBadgeCursor} from "../../shared/tag-badge/tag-badge.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LongClickDirective} from "../../_directives/long-click.directive";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

type KeyBindFormGroup = FormGroup<{
  [K in KeyBindTarget]: FormArray<FormControl<string[]>>
}>;

@Component({
  selector: 'app-manage-custom-key-binds',
  imports: [
    ReactiveFormsModule,
    SettingItemComponent,
    SettingKeyBindPickerComponent,
    TagBadgeComponent,
    DefaultValuePipe,
    LongClickDirective,
    NgbTooltip
  ],
  templateUrl: './manage-custom-key-binds.component.html',
  styleUrl: './manage-custom-key-binds.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageCustomKeyBindsComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly keyBindService = inject(KeyBindService);
  private readonly transLoco = inject(TranslocoService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected keyBindForm!: KeyBindFormGroup;

  protected selectedIndexes = signal<Map<string, number>>(new Map());

  ngOnInit(): void {
    const keyBinds = this.keyBindService.allKeyBinds();
    const groupConfig = Object.entries(keyBinds).reduce((acc, [key, value]) => {
      acc[key as KeyBindTarget] = this.fb.array(this.toFormControls(value));
      return acc;
    }, {} as Record<KeyBindTarget, FormArray<FormControl<string[]>>>);

    this.keyBindForm = this.fb.group(groupConfig);

    this.keyBindForm.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(500),
      distinctUntilChanged(),
      filter(() => this.keyBindForm.valid),
      map((customKeyBinds) => {
        return {
          ...this.accountService.currentUserSignal()!.preferences,
          customKeyBinds,
        }
      }),
      switchMap(p => this.accountService.updatePreferences(p))
    ).subscribe();
  }

  private toFormControls(combos: string[][]): FormControl<string[]>[] {
    return combos.map(combo => this.fb.control(combo, this.keyBindComboValidator()));
  }

  getFormArray(key: string): FormArray<FormControl<string[]>> | null {
    return this.keyBindForm.get(key) as FormArray<FormControl<string[]>> | null;
  }

  resetKeybindsToDefaults(key: string) {
    const array = this.getFormArray(key);
    if (!array) return;

    array.clear()
    array.push(this.toFormControls(DefaultKeyBinds[key as KeyBindTarget]))
  }

  selectIndex(key: string, index: number) {
    this.selectedIndexes.update(m => {
      const newMap = new Map(m);
      newMap.set(key, index);
      return newMap;
    })
  }

  addKeyBind(key: string) {
    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length < 5) {
      array.push(this.fb.control([], this.keyBindComboValidator()));
    }
  }

  removeKeyBind(key: string, index: number) {
    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length === 1) {
      this.resetKeybindsToDefaults(key);
    } else {
      array.removeAt(index)
    }
  }

  private keyBindComboValidator(): ValidatorFn {
    return (control) => {
      const combo = (control as FormControl<string[]>).value;
      if (combo.length === 0) return { 'need-at-least-one-key': {'length': 0} } as ValidationErrors;

      if (combo.filter(key => !ModifierKeyCodes.includes(key as KeyCode)).length === 0) {
        return { 'non-modifier-required': { 'combo': combo } } as ValidationErrors;
      }

      if (this.keyBindService.isReservedKeyCombo(combo)) {
        return { 'reserved-combo': { 'combo': combo }} as ValidationErrors
      }

      return null;
    }
  }

  protected errorToolTip(errors: ValidationErrors | null): string | null {
    if (!errors) return null;

    let toolTip = '';
    for (let key of Object.keys(errors)) {
      toolTip += ' ' + this.transLoco.translate('custom-key-binds.combo-error-' + key)
    }
    return toolTip.length > 0 ? toolTip.trim() : null;
  }

  protected t(key: string, params?: any) {
    key = `custom-key-binds.${key}`
    const translation = this.transLoco.translate(key, params);
    if (translation === key) {
      return '';
    }

    return translation;
  }

  protected readonly Object = Object;
  protected readonly DefaultKeyBinds = DefaultKeyBinds;
  protected readonly getReadableComboLabel = getReadableComboLabel;
  protected readonly TagBadgeCursor = TagBadgeCursor;
}
