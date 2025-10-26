import {ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnInit, Signal} from '@angular/core';
import {DefaultKeyBinds, KeyBindService} from "../../_services/key-bind.service";
import {FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {KeyBindTarget} from "../../_models/preferences/preferences";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  SettingKeyBindPickerComponent
} from "../../settings/_components/setting-key-bind-picker/setting-key-bind-picker.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, switchMap, tap} from "rxjs";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";

type KeyBindFormGroup = FormGroup<{
  [K in KeyBindTarget]: FormControl<string[]>
}>;

@Component({
  selector: 'app-manage-custom-key-binds',
  imports: [
    ReactiveFormsModule,
    SettingItemComponent,
    SettingKeyBindPickerComponent
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

  ngOnInit(): void {
    const keyBinds = this.keyBindService.allKeyBinds();
    const groupConfig = Object.entries(keyBinds).reduce((acc, [key, value]) => {
      acc[key as KeyBindTarget] = this.fb.control<string[]>(value);
      return acc;
    }, {} as Record<KeyBindTarget, FormControl<string[]>>);

    this.keyBindForm = this.fb.group(groupConfig);

    this.keyBindForm.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(500),
      distinctUntilChanged(),
      map((customKeyBinds) => {
        return {
          ...this.accountService.currentUserSignal()!.preferences,
          customKeyBinds,
        }
      }),
      switchMap(p => this.accountService.updatePreferences(p))
    ).subscribe();
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
}
