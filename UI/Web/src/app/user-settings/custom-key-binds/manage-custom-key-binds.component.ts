import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {DefaultKeyBinds, KeyBindGroups, KeyBindService, KeyCode,} from "../../_services/key-bind.service";
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn
} from "@angular/forms";
import {KeyBind, KeyBindTarget, Preferences} from "../../_models/preferences/preferences";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  SettingKeyBindPickerComponent
} from "../../settings/_components/setting-key-bind-picker/setting-key-bind-picker.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap} from "rxjs";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LongClickDirective} from "../../_directives/long-click.directive";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";
import {LicenseService} from "../../_services/license.service";
import {KeybindSettingDescriptionPipe} from "../../_pipes/keybind-setting-description.pipe";
import {DOCUMENT} from "@angular/common";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";

type KeyBindFormGroup = FormGroup<{
  [K in KeyBindTarget]: FormArray<FormControl<KeyBind>>
}>;

const MAX_KEYBINDS_PER_TARGET = 5;

@Component({
  selector: 'app-manage-custom-key-binds',
  imports: [
    ReactiveFormsModule,
    SettingItemComponent,
    SettingKeyBindPickerComponent,
    DefaultValuePipe,
    NgbTooltip,
    KeybindSettingDescriptionPipe,
    TranslocoDirective,
    LongClickDirective,
    SafeHtmlPipe
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
  private readonly licenseService = inject(LicenseService);
  private readonly document = inject(DOCUMENT);

  protected keyBindForm!: KeyBindFormGroup;

  protected duplicatedKeyBinds = signal<Partial<Record<KeyBindTarget, number[]>>>({});
  protected filteredKeyBindGroups = computed(() => {
    const roles = this.accountService.currentUserSignal()!.roles;
    const hasKPlus = this.licenseService.hasValidLicenseSignal();

    return KeyBindGroups.map(g => {
      g.elements = g.elements.filter(e => {
        if (e.roles && !e.roles.some(r => roles.includes(r))) return false;
        if (e.restrictedRoles && e.restrictedRoles.some(r => roles.includes(r))) return false;

        return hasKPlus || !e.kavitaPlus;
      })
      return g;
    }).filter(g => g.elements.length > 0);
  });

  ngOnInit() {
    const keyBinds = this.keyBindService.allKeyBinds();
    const groupConfig = Object.entries(keyBinds).reduce((acc, [key, value]) => {
      acc[key as KeyBindTarget] = this.fb.array(this.toFormControls(value), this.keyBindArrayValidator());
      return acc;
    }, {} as Record<KeyBindTarget, FormArray<FormControl<KeyBind>>>);

    this.keyBindForm = this.fb.group(groupConfig);
    this.duplicatedKeyBinds.set(this.extractDuplicated(keyBinds)); // Set initial

    this.keyBindForm.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(250),
      distinctUntilChanged(),
      map(formValue => this.extractDuplicated(formValue)),
      tap(d => this.duplicatedKeyBinds.set(d)),
    ).subscribe();

    this.keyBindForm.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(500),
      distinctUntilChanged(),
      filter(() => this.keyBindForm.valid),
      map(formValue => this.extractCustomKeyBinds(formValue)),
      map(customKeyBinds => this.combinePreferences(customKeyBinds)),
      switchMap(p => this.accountService.updatePreferences(p)),
      catchError(err => {
        console.error(err);
        this.toastr.error(err);

        return of(null);
      }),
    ).subscribe();
  }

  private extractDuplicated(formValue: Partial<Record<KeyBindTarget, KeyBind[]>>): Partial<Record<KeyBindTarget, number[]>> {
    const entries = Object.entries(formValue);

    return Object.fromEntries(entries
        .map(([target, keyBinds]) => {
          const duplicatedIndices = keyBinds.map((keyBind, index) => {
              const isDuplicated = entries.some(([otherTarget, otherKeyBinds]) => {
                if (otherTarget === target) return false;

                return otherKeyBinds.some(kb => this.keyBindService.areKeyBindsEqual(keyBind, kb));
              });

              return isDuplicated ? index : -1;
            })
            .filter(index => index !== -1) ?? [];

          return [target, duplicatedIndices];
        })
      .filter(([_, indices]) => (indices as number[]).length > 0)
    ) as Partial<Record<KeyBindTarget, number[]>>;
  }

  private extractCustomKeyBinds(formValue: Partial<Record<KeyBindTarget, KeyBind[]>>): Partial<Record<KeyBindTarget, KeyBind[]>> {
    return Object.fromEntries(
      Object.entries(formValue).filter(([target, keybinds]) =>
        !this.keyBindService.isDefaultKeyBinds(target as KeyBindTarget, keybinds)
      )
    ) as Partial<Record<KeyBindTarget, KeyBind[]>>;
  }

  private combinePreferences(customKeyBinds: Partial<Record<KeyBindTarget, KeyBind[]>>): Preferences {
    return {
      ...this.accountService.currentUserSignal()!.preferences,
      customKeyBinds,
    };
  }

  private toFormControls(keybinds: KeyBind[]): FormControl<KeyBind>[] {
    return keybinds.map(keyBind => this.fb.control(keyBind, this.keyBindValidator()));
  }

  trackByKeyBind(index: number, keyBind: KeyBind) {
    let key = `${index}_${keyBind.key}_ctrl_${keyBind.control}_meta_${keyBind.meta}_alt_${keyBind.alt}_shift_${keyBind.shift}`;
    if (keyBind.controllerSequence) {
      key += `controller_${keyBind.controllerSequence.join('_')}`;
    }
    return key;
  }

  /**
   * Typed getter for the FormArray of a given target
   * @param key
   */
  getFormArray(key: KeyBindTarget): FormArray<FormControl<KeyBind>> | null {
    return this.keyBindForm.get(key) as FormArray<FormControl<KeyBind>> | null;
  }

  /**
   * Reset keybinds to default configured values
   * @param key
   */
  resetKeybindsToDefaults(key: KeyBindTarget) {
    if (this.accountService.isReadOnly()) return;

    this.keyBindForm.setControl(key, this.fb.array(this.toFormControls(DefaultKeyBinds[key]), this.keyBindArrayValidator()));
  }

  /**
   * Add a new keybind option to the array, NOP if MAX_KEYBINDS_PER_TARGET has been reached
   * @param key
   */
  addKeyBind(key: KeyBindTarget) {
    if (this.accountService.isReadOnly()) return;

    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length < MAX_KEYBINDS_PER_TARGET) {
      array.push(this.fb.control({key: KeyCode.Empty}, this.keyBindValidator()));
    }

    setTimeout(() => {
      const id = `key-bind-${key}-${array.length-1}`;
      const newElement = this.document.getElementById(id);
      if (newElement) {
        newElement.focus();
      }

    }, 100);
  }

  /**
   * Remove a keybind from the array, if this is the last keybind. Resets to default
   * @param key
   * @param index
   */
  removeKeyBind(key: KeyBindTarget, index: number) {
    if (this.accountService.isReadOnly()) return;

    const array = this.getFormArray(key);
    if (!array) return;

    if (array.controls.length === 1) {
      this.resetKeybindsToDefaults(key);
    } else {
      array.removeAt(index)
    }
  }

  /**
   * Custom validator for FormControl<KeyBind>
   * @private
   */
  private keyBindValidator(): ValidatorFn {
    return (control) => {
      const keyBind = (control as FormControl<KeyBind>).value;
      if (keyBind.key.length === 0 && !keyBind.controllerSequence) return { 'need-at-least-one-key': {'length': 0} } as ValidationErrors;

      if (this.keyBindService.isReservedKeyBind(keyBind)) {
        return { 'reserved-key-bind': { 'keyBind': keyBind }} as ValidationErrors
      }

      return null;
    }
  }

  private keyBindArrayValidator(): ValidatorFn {
    return (control) => {
      const controls = (control as FormArray<FormControl<KeyBind>>).controls;

      const anyOverlap = controls.some((c, i) => controls.some((c2, i2)=> {
        return i !== i2 && this.keyBindService.areKeyBindsEqual(c.value, c2.value);
      }))

      if (anyOverlap) {
        return { 'overlap-in-target': { '': '' } }
      }

      return null;

    }
  }

  /**
   * Combined tooltip for FormControl<KeyBind> errors
   * @param target
   * @param index
   * @param errors
   * @protected
   */
  protected errorToolTip(target: KeyBindTarget, index: number, errors: ValidationErrors | null): string | null {
    if (errors) {
      return Object.keys(errors)
        .map(key => this.transLoco.translate(`manage-custom-key-binds.key-bind-error-${key}`))
        .join(' ')
        .trim() || null;
    }

    if (this.duplicatedKeyBinds()[target]?.includes(index)) {
      return this.transLoco.translate('manage-custom-key-binds.warning-duplicate-key-bind');
    }

    return null;
  }

  protected readonly Object = Object;
  protected readonly MAX_KEYBINDS_PER_TARGET = MAX_KEYBINDS_PER_TARGET;
}
