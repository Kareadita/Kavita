import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ServerSettings} from "../_models/server-settings";
import {
  AbstractControl,
  AsyncValidatorFn,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn
} from "@angular/forms";
import {SettingsService} from "../settings.service";
import {OidcConfig} from "../_models/oidc-config";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {debounceTime, distinctUntilChanged, filter, forkJoin, map, of, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRating} from "../../_models/metadata/age-rating";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AccountService, allRoles, Role} from "../../_services/account.service";
import {Library} from "../../_models/library/library";
import {LibraryService} from "../../_services/library.service";
import {ToastrService} from "ngx-toastr";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";

type OidcFormGroup = FormGroup<{
  autoLogin: FormControl<boolean>;
  disablePasswordAuthentication: FormControl<boolean>;
  providerName: FormControl<string>;
  authority: FormControl<string>;
  clientId: FormControl<string>;
  secret: FormControl<string>;
  provisionAccounts: FormControl<boolean>;
  requireVerifiedEmail: FormControl<boolean>;
  syncUserSettings: FormControl<boolean>;
  rolesPrefix: FormControl<string>;
  rolesClaim: FormControl<string>;
  customScopes: FormControl<string[]>;
  defaultRoles: FormControl<string[]>;
  defaultLibraries: FormControl<number[]>;
  defaultAgeRestriction: FormControl<AgeRating>;
  defaultIncludeUnknowns: FormControl<boolean>;
}>;

@Component({
  selector: 'app-manage-open-idconnect',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    SettingSwitchComponent,
    AgeRatingPipe,
    SafeHtmlPipe,
    DefaultValuePipe,
    SettingMultiCheckBox,
    SettingMultiTextFieldComponent
  ],
  templateUrl: './manage-open-idconnect.component.html',
  styleUrl: './manage-open-idconnect.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageOpenIDConnectComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly metadataService = inject(MetadataService);
  private readonly toastr = inject(ToastrService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);

  serverSettings!: ServerSettings;
  settingsForm!: OidcFormGroup;

  loading = signal(true);
  oidcSettings = signal<OidcConfig | undefined>(undefined);
  ageRatings = signal<AgeRatingDto[]>([]);
  libraries = signal<Library[]>([]);
  libraryOptions = computed(() => this.libraries().map(l => {
    return { label: l.name, value: l.id };
  }));
  roles = signal<Role[]>(allRoles);
  roleOptions: MultiCheckBoxItem<Role>[] = allRoles.map(r => {
    return { label: r, value: r, disableFunc: (r, selected) => {
      return r !== Role.Admin && selected.includes(Role.Admin);
    }}
  });

  ngOnInit(): void {
    forkJoin([
      this.metadataService.getAllAgeRatings(),
      this.settingsService.getServerSettings(),
      this.libraryService.getLibraries(),
    ]).subscribe(([ageRatings, settings, libraries]) => {
      this.ageRatings.set(ageRatings);
      this.libraries.set(libraries);

      this.serverSettings = settings;
      this.oidcSettings.set(this.serverSettings.oidcConfig);

      this.settingsForm = this.fb.group({
        authority: this.fb.control(this.serverSettings.oidcConfig.authority, { asyncValidators: [this.authorityValidator()] }),
        clientId: this.fb.control(this.serverSettings.oidcConfig.clientId, { validators: [this.requiredIf('authority')] }),
        secret: this.fb.control(this.serverSettings.oidcConfig.secret, { validators: [this.requiredIf('authority')] }),
        provisionAccounts: this.fb.control(this.serverSettings.oidcConfig.provisionAccounts),
        requireVerifiedEmail: this.fb.control(this.serverSettings.oidcConfig.requireVerifiedEmail),
        syncUserSettings: this.fb.control(this.serverSettings.oidcConfig.syncUserSettings),
        rolesPrefix: this.fb.control(this.serverSettings.oidcConfig.rolesPrefix),
        rolesClaim: this.fb.control(this.serverSettings.oidcConfig.rolesClaim),
        autoLogin: this.fb.control(this.serverSettings.oidcConfig.autoLogin),
        disablePasswordAuthentication: this.fb.control(this.serverSettings.oidcConfig.disablePasswordAuthentication),
        providerName: this.fb.control(this.serverSettings.oidcConfig.providerName),
        defaultLibraries: this.fb.control(this.serverSettings.oidcConfig.defaultLibraries),
        defaultRoles: this.fb.control(this.serverSettings.oidcConfig.defaultRoles),
        defaultAgeRestriction: this.fb.control(this.serverSettings.oidcConfig.defaultAgeRestriction),
        defaultIncludeUnknowns: this.fb.control(this.serverSettings.oidcConfig.defaultIncludeUnknowns),
        customScopes: this.fb.control(this.serverSettings.oidcConfig.customScopes)
      });

      this.loading.set(false);
      this.cdRef.markForCheck();

      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
        filter(() => {
          // Do not auto save when provider settings have changed
          const settings: OidcConfig = this.packData().oidcConfig;
          return settings.authority == this.oidcSettings()?.authority && settings.clientId == this.oidcSettings()?.clientId;
        }),
        tap(() => this.save())
      ).subscribe();
    })
  }

  private packData(): ServerSettings {
    const newSettings = Object.assign({}, this.serverSettings);
    newSettings.oidcConfig = {
      ...this.settingsForm.getRawValue(),
      enabled: false,
    };
    return newSettings;
  }

  save(showConfirmation: boolean = false) {
    if (!this.settingsForm.valid || !this.serverSettings || !this.oidcSettings()) return;

    const newSettings = this.packData();
    this.settingsService.updateServerSettings(newSettings).subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings.set(data.oidcConfig);
        this.cdRef.markForCheck();

        if (showConfirmation) {
          this.toastr.success(translate('manage-oidc-connect.save-success'))
        }
      },
      error: error => {
        console.error(error);
        this.toastr.error(translate('errors.generic'))
      }
    })
  }

  authorityValidator(): AsyncValidatorFn {
    return (control: AbstractControl) => {
      let uri: string = control.value;
      if (!uri || uri.trim().length === 0) {
        return of(null);
      }

      try {
        new URL(uri);
      } catch {
        return of({'invalidUri': {'uri': uri}} as ValidationErrors)
      }

      return this.settingsService.ifValidAuthority(uri).pipe(map(ok => {
        if (ok) return null;

        return {'invalidUri': {'uri': uri}} as ValidationErrors;
      }));
    }
  }

  requiredIf(other: string): ValidatorFn {
    return (control): ValidationErrors | null => {
      if (!this.settingsForm) return null;

      const otherControl = this.settingsForm.get(other);
      if (!otherControl) return null;

      if (otherControl.invalid) return null;

      const v = otherControl.value;
      if (!v || v.length === 0) return null;

      const own = control.value;
      if (own && own.length > 0) return null;

      return {'requiredIf': {'other': other, 'otherValue': v}}
    }
  }

}
