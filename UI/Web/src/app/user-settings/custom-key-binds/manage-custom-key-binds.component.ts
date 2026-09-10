import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef, effect,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {DefaultKeyBinds, KeyBindGroups, KeyBindService, KeyCode,} from "../../_services/key-bind.service";
import {KeyBind, KeyBindTarget, Preferences} from "../../_models/preferences/preferences";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  SettingKeyBindPickerComponent
} from "../../settings/_components/setting-key-bind-picker/setting-key-bind-picker.component";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap} from "rxjs";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LongClickDirective} from "../../_directives/long-click.directive";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from '@openng/ngx-toastr';
import {LicenseService} from "../../_services/license.service";
import {KeybindSettingDescriptionPipe} from "../../_pipes/keybind-setting-description.pipe";
import {DOCUMENT} from "@angular/common";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {
  applyEach,
  debounce, disabled,
  form, FormField,
  PathKind,
  SchemaPath,
  SchemaPathTree,
  validate, ValidationError
} from "@angular/forms/signals";
const MAX_KEYBINDS_PER_TARGET = 5;

type FormModel = {
  [K in KeyBindTarget]: KeyBind[];
};

@Component({
  selector: 'app-manage-custom-key-binds',
  imports: [
    SettingItemComponent,
    SettingKeyBindPickerComponent,
    DefaultValuePipe,
    NgbTooltip,
    KeybindSettingDescriptionPipe,
    TranslocoDirective,
    LongClickDirective,
    SafeHtmlPipe,
    FormField
  ],
  templateUrl: './manage-custom-key-binds.component.html',
  styleUrl: './manage-custom-key-binds.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageCustomKeyBindsComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  protected readonly keyBindService = inject(KeyBindService);
  private readonly transLoco = inject(TranslocoService);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly licenseService = inject(LicenseService);
  private readonly document = inject(DOCUMENT);
  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;

  protected formModel = signal<FormModel>({
    [KeyBindTarget.NavigateToSettings]: [],
    [KeyBindTarget.OpenSearch]: [],
    [KeyBindTarget.NavigateToScrobbling]: [],
    [KeyBindTarget.ToggleFullScreen]: [],
    [KeyBindTarget.BookmarkPage]: [],
    [KeyBindTarget.OpenHelp]: [],
    [KeyBindTarget.GoTo]: [],
    [KeyBindTarget.ToggleMenu]: [],
    [KeyBindTarget.PageLeft]: [],
    [KeyBindTarget.PageRight]: [],
    [KeyBindTarget.Escape]: [],
    [KeyBindTarget.PageUp]: [],
    [KeyBindTarget.PageDown]: [],
    [KeyBindTarget.OffsetDoublePage]: [],
    [KeyBindTarget.NextChapter]: [],
    [KeyBindTarget.PreviousChapter]: [],
    [KeyBindTarget.FirstPage]: [],
    [KeyBindTarget.LastPage]: [],
    [KeyBindTarget.NavigateHome]: [],
  });

  protected formGroup = form(this.formModel, path => {
    debounce(path, 250);
    disabled(path, { when: () => this.accountService.hasReadOnlyRole()});

    for (const target of Object.values(KeyBindTarget)) {
      this.validateKeybinds(path[target]);
      applyEach(path[target], item => {
        this.validateKeybind(item);
      });
    }
  });

  protected duplicatedKeyBinds = signal<Partial<Record<KeyBindTarget, number[]>>>({});
  protected filteredKeyBindGroups = computed(() => {
    const roles = this.accountService.currentUser()!.roles;
    const hasKPlus = this.licenseService.hasActiveLicense();

    return KeyBindGroups.map(g => {
      g.elements = g.elements.filter(e => {
        if (e.roles && !e.roles.some(r => roles.includes(r))) return false;
        if (e.restrictedRoles && e.restrictedRoles.some(r => roles.includes(r))) return false;

        return hasKPlus || !e.kavitaPlus;
      })
      return g;
    }).filter(g => g.elements.length > 0);
  });

  constructor() {
    effect(() => {
      const keyBinds = this.formModel();
      const duplicateKeys = this.extractDuplicated(keyBinds);

      this.duplicatedKeyBinds.set(duplicateKeys);
    });

    toObservable(this.formModel).pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(250),
      distinctUntilChanged(),
      filter(() => this.formGroup().valid()),
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

  ngOnInit() {
    const keyBinds = this.keyBindService.allKeyBinds();
    this.formModel.set(keyBinds);

    this.duplicatedKeyBinds.set(this.extractDuplicated(keyBinds)); // Set initial
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
      ...this.accountService.currentUser()!.preferences,
      customKeyBinds,
    };
  }

  /**
   * Reset keybinds to default configured values
   * @param key
   */
  resetKeybindsToDefaults(key: KeyBindTarget) {
    if (this.accountService.hasReadOnlyRole()) return;

    this.formModel.update(model => ({
      ...model,
      [key]: DefaultKeyBinds[key],
    }));
  }

  /**
   * Add a new keybind option to the array, NOP if MAX_KEYBINDS_PER_TARGET has been reached
   * @param key
   */
  addKeyBind(key: KeyBindTarget) {
    if (this.accountService.hasReadOnlyRole()) return;

    const keyBinds = this.formModel()[key];
    if (keyBinds.length >= MAX_KEYBINDS_PER_TARGET) {
      return;
    }

    this.formModel.update(model => ({
      ...model,
      [key]: [...keyBinds, {key: KeyCode.Empty}]
    }));

    setTimeout(() => {
      const id = `key-bind-${key}-${keyBinds.length}`;
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
    if (this.accountService.hasReadOnlyRole()) return;

    const keyBinds = this.formModel()[key];
    if (keyBinds.length === 1) {
      this.resetKeybindsToDefaults(key);
      return;
    }

    this.formModel.update(model => ({
      ...model,
      [key]: model[key].filter((_, i) => i !== index),
    }));
  }

  private validateKeybind(path: SchemaPathTree<KeyBind, PathKind.Item>) {
    validate(path, ctx => {
      const keybind = ctx.value();
      if (keybind.key.length === 0 && !keybind.controllerSequence) {
        return {
          kind: 'need-at-least-one-key',
        }
      }

      if (this.keyBindService.isReservedKeyBind(keybind)) {
        return {
          kind: 'reserved-key-bind'
        }
      }

      return null;
    });
  }

  private validateKeybinds(path: SchemaPath<KeyBind[]>) {
    validate(path, ctx => {
      const keybinds = ctx.value();

      const anyOverlap = keybinds.some((c, i) => keybinds.some((c2, i2) => {
        return i !== i2 && this.keyBindService.areKeyBindsEqual(c, c2);
      }));

      if (anyOverlap) {
        return {
          kind: 'overlap-in-target'
        }
      }

      return null;
    });
  }

  /**
   * Combined tooltip for FormControl<KeyBind> errors
   * @param target
   * @param index
   * @param errors
   * @protected
   */
  protected errorToolTip(target: KeyBindTarget, index: number, errors: ValidationError[]): string | null {
    if (errors.length > 0) {
      return errors
        .map(error => this.transLoco.translate(`manage-custom-key-binds.key-bind-error-${error.kind}`))
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
