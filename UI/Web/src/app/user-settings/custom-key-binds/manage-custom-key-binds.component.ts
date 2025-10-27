import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {
  AllKeyBindTargets,
  DefaultKeyBinds,
  KeyBindService,
  KeyCode,
  KeyCombo,
  ModifierKeyCodes
} from "../../_services/key-bind.service";
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn
} from "@angular/forms";
import {KeyBindTarget} from "../../_models/preferences/preferences";
import {TranslocoService} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  SettingKeyBindPickerComponent
} from "../../settings/_components/setting-key-bind-picker/setting-key-bind-picker.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap} from "rxjs";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {TagBadgeComponent, TagBadgeCursor} from "../../shared/tag-badge/tag-badge.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LongClickDirective} from "../../_directives/long-click.directive";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";

type KeyBindFormGroup = FormGroup<{
  [K in KeyBindTarget]: FormArray<FormControl<KeyCombo>>
}>;

const MAX_KEYBINDS_PER_TARGET = 5;

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
  protected readonly keyBindService = inject(KeyBindService);
  private readonly transLoco = inject(TranslocoService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);

  protected keyBindForm!: KeyBindFormGroup;

  protected selectedIndexes = signal<Map<string, number>>(new Map());

  ngOnInit(): void {
    const keyBinds = this.keyBindService.allKeyBinds();
    const groupConfig = Object.entries(keyBinds).reduce((acc, [key, value]) => {
      acc[key as KeyBindTarget] = this.fb.array(this.toFormControls(value));
      return acc;
    }, {} as Record<KeyBindTarget, FormArray<FormControl<KeyCombo>>>);

    this.keyBindForm = this.fb.group(groupConfig);

    this.keyBindForm.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(500),
      distinctUntilChanged(),
      filter(() => this.keyBindForm.valid),
      map(customKeyBinds => Object.fromEntries(
        Object.entries(customKeyBinds).filter(([target, combos]) => {
          return !this.keyBindService.isDefaultKeyBinds(target as KeyBindTarget, combos);
        })
      ) as Partial<Record<KeyBindTarget, KeyCombo[]>>),
      map((customKeyBinds) => {
        return {
          ...this.accountService.currentUserSignal()!.preferences,
          customKeyBinds,
        }
      }),
      switchMap(p => this.accountService.updatePreferences(p)),
      catchError(err => {
        this.toastr.error(err)
        return of(null);
      }),
    ).subscribe();
  }

  private toFormControls(combos: KeyCombo[]): FormControl<KeyCombo>[] {
    return combos.map(combo => this.fb.control(combo, this.keyBindComboValidator()));
  }

  /**
   * Typed getting for the FormArray
   * @param key
   */
  getFormArray(key: KeyBindTarget): FormArray<FormControl<KeyCombo>> | null {
    return this.keyBindForm.get(key) as FormArray<FormControl<KeyCombo>> | null;
  }

  /**
   * Reset keybinds to default configured values
   * @param key
   */
  resetKeybindsToDefaults(key: KeyBindTarget) {
    const array = this.getFormArray(key);
    if (!array) return;

    array.clear();
    array.push(this.toFormControls(DefaultKeyBinds[key as KeyBindTarget]));
  }

  /**
   * Select which FromControl to display in edit mode
   * @param key
   * @param index
   */
  selectIndex(key: KeyBindTarget, index: number) {
    this.selectedIndexes.update(m => {
      const newMap = new Map(m);
      newMap.set(key, index);
      return newMap;
    });
  }

  /**
   * Add a new keybind option to the array, NOP if MAX_KEYBINDS_PER_TARGET has been reached
   * @param key
   */
  addKeyBind(key: KeyBindTarget) {
    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length < MAX_KEYBINDS_PER_TARGET) {
      array.push(this.fb.control([], this.keyBindComboValidator()));
    }
  }

  /**
   * Remove a keybind from the array, if this is the last keybind. Resets to default
   * @param key
   * @param index
   */
  removeKeyBind(key: KeyBindTarget, index: number) {
    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length === 1) {
      this.resetKeybindsToDefaults(key);
    } else {
      array.removeAt(index)
    }
  }

  /**
   * Custom validator for FormControl<KeyCombo>
   * @private
   */
  private keyBindComboValidator(): ValidatorFn {
    return (control) => {
      const combo = (control as FormControl<KeyCombo>).value;
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

  /**
   * Combined tooltip for FormControl<KeyCombo> errors
   * @param errors
   * @protected
   */
  protected errorToolTip(errors: ValidationErrors | null): string | null {
    if (!errors) return null;

    let toolTip = '';
    for (let key of Object.keys(errors)) {
      toolTip += ' ' + this.transLoco.translate('custom-key-binds.combo-error-' + key)
    }
    return toolTip.length > 0 ? toolTip.trim() : null;
  }

  /**
   * Wrapper around TransLocoService#translate to allow for empty translations
   * @param key
   * @param params
   * @protected
   */
  protected t(key: string, params?: any) {
    key = `custom-key-binds.${key}`
    const translation = this.transLoco.translate(key, params);
    if (translation === key) {
      return '';
    }

    return translation;
  }

  protected readonly Object = Object;
  protected readonly TagBadgeCursor = TagBadgeCursor;
  protected readonly AllKeyBindTargets = AllKeyBindTargets;
  protected readonly MAX_KEYBINDS_PER_TARGET = MAX_KEYBINDS_PER_TARGET;
}
