import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef, effect,
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
import {AuthorityValidationResult, OidcConfig} from "../_models/oidc-config";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {debounceTime, distinctUntilChanged, filter, forkJoin, map, of, skip, tap} from "rxjs";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRating} from "../../_models/metadata/age-rating";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AccountService, allRoles, Role} from "../../_services/account.service";
import {Library} from "../../_models/library/library";
import {LibraryService} from "../../_services/library.service";
import {ToastrService} from '@openng/ngx-toastr';
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";
import {environment} from "../../../environments/environment";
import {SlicePipe} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ConfirmService} from "../../shared/confirm.service";
import {AuthorityValidationResultPipe} from "../../_pipes/authority-validation-result.pipe";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {disabled, form, FormField, metadata, validateAsync} from "@angular/forms/signals";
import {SettingEnumSelectComponent} from "../../settings/_components/setting-enum-select/setting-enum-select.component";
import {REQUIRED_IF_NAME, requiredIf} from "../../_validators/requiredIf.validator";
import {url} from "../../_validators/url.validator";

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
    SettingMultiTextFieldComponent,
    SlicePipe,
    NgbTooltip,
    FormFieldDirective,
    FormField,
    SettingEnumSelectComponent
  ],
  templateUrl: './manage-open-idconnect.component.html',
  styleUrl: './manage-open-idconnect.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageOpenIDConnectComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private readonly toastr = inject(ToastrService);
  private readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);
  private readonly confirmService = inject(ConfirmService);

  private readonly authorityValidationResultPipe = new AuthorityValidationResultPipe();

  serverSettings!: ServerSettings;

  oidcSettingsFormModel = signal<OidcConfig>({
    authority: "",
    autoLogin: false,
    clientId: "",
    customScopes: [],
    defaultAgeRestriction: AgeRating.NotApplicable,
    defaultIncludeUnknowns: false,
    defaultLibraries: [],
    defaultRoles: [],
    disablePasswordAuthentication: false,
    enabled: false,
    providerName: "",
    provisionAccounts: false,
    requireVerifiedEmail: false,
    rolesClaim: "",
    rolesPrefix: "",
    secret: "",
    syncUserSettings: false

  });
  oidcSettingsFormGroup = form(this.oidcSettingsFormModel, (path) => {
    disabled(path, {when: () => !this.accountService.hasAdminRole()});

    url(path.authority, { requireTls: true });
    metadata(path.authority, REQUIRED_IF_NAME, () => translate('manage-oidc-connect.authority-label'));
    validateAsync(path.authority, {
      params: ({value}) => {
        const url = value();
        if (url == null || !url || url.trim().length === 0) {
          return undefined;
        }
        return url;
      },
      factory: (authority) => {
        return this.settingsService.validAuthorityRsc(authority);
      },
      onError: error => {},
      onSuccess: result => {
        if (result === AuthorityValidationResult.Success || result === AuthorityValidationResult.NotApplicable) {
          return null;
        }

        return {
          kind: 'backendFailure',
          message: this.authorityValidationResultPipe.transform(result),
        }
      }
    });

    requiredIf(path.clientId, path.authority);
    requiredIf(path.secret, path.authority);
  });

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
  autoSavingBlocked = signal(false);

  constructor() {
    toObservable(this.oidcSettingsFormModel).pipe(
      skip(2),
      debounceTime(300),
      distinctUntilChanged(),
      filter(() => this.oidcSettingsFormGroup().valid()),
      filter(() => {
        const settings: OidcConfig = this.packData().oidcConfig;
        const autoSave = settings.authority == this.oidcSettings()?.authority && settings.clientId == this.oidcSettings()?.clientId;

        this.autoSavingBlocked.set(!autoSave);
        return autoSave;
      }),
      tap(() => this.save())
    ).subscribe();
  }

  ngOnInit(): void {
    forkJoin([
      this.metadataService.getAllAgeRatings(),
      this.settingsService.getServerSettings(),
      this.libraryService.getLibraries(),
    ]).subscribe(([ageRatings, settings, libraries]) => {
      this.ageRatings.set(ageRatings);
      this.libraries.set(libraries);

      this.serverSettings = settings;
      this.oidcSettingsFormModel.set(this.serverSettings.oidcConfig);
      this.oidcSettings.set(this.serverSettings.oidcConfig);

      this.loading.set(false);
    })
  }

  private packData(): ServerSettings {
    const newSettings = Object.assign({}, this.serverSettings);
    newSettings.oidcConfig = {
      ...this.oidcSettingsFormModel(),
      enabled: false,
    };
    return newSettings;
  }

  async resetIds() {
    if (!await this.confirmService.confirm(translate('manage-oidc-connect.reset-confirm'))) {
      return;
    }

    this.settingsService.clearExternalIds().subscribe(() => this.toastr.info(translate('manage-oidc-connect.reset-success')))


  }

  save(showToasts: boolean = false) {
    if (!this.oidcSettingsFormGroup().valid()) {
      if (showToasts) {
        this.toastr.error(translate('errors.invalid-form'));
      }

      return;
    }

    if (!this.serverSettings || !this.oidcSettings()) return;

    const newSettings = this.packData();
    this.settingsService.updateServerSettings(newSettings).subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings.set(data.oidcConfig);
        this.cdRef.markForCheck();

        if (showToasts) {
          this.toastr.success(translate('manage-oidc-connect.save-success'))
        }
      },
      error: error => {
        console.error(error);
        this.toastr.error(translate('errors.generic'))
      }
    })
  }

}
