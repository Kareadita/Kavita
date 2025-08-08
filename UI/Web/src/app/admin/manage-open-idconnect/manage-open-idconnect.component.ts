import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
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
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn
} from "@angular/forms";
import {SettingsService} from "../settings.service";
import {OidcConfig} from "../_models/oidc-config";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {debounceTime, distinctUntilChanged, filter, map, of, switchMap, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {RestrictionSelectorComponent} from "../../user-settings/restriction-selector/restriction-selector.component";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRating} from "../../_models/metadata/age-rating";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {allRoles, Role} from "../../_services/account.service";
import {Library} from "../../_models/library/library";
import {LibraryService} from "../../_services/library.service";
import {LibrarySelectorComponent} from "../library-selector/library-selector.component";
import {RoleSelectorComponent} from "../role-selector/role-selector.component";
import {ToastrService} from "ngx-toastr";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";

@Component({
  selector: 'app-manage-open-idconnect',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    SettingSwitchComponent,
    AgeRatingPipe,
    LibrarySelectorComponent,
    RoleSelectorComponent,
    SafeHtmlPipe,
    DefaultValuePipe,
    TagBadgeComponent
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

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});

  oidcSettings = signal<OidcConfig | undefined>(undefined);
  ageRatings = signal<AgeRatingDto[]>([]);
  selectedLibraries = signal<number[]>([]);
  selectedRoles = signal<string[]>([]);

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });

    this.settingsService.getServerSettings().subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings.set(this.serverSettings.oidcConfig);
        this.selectedRoles.set(this.serverSettings.oidcConfig.defaultRoles);
        this.selectedLibraries.set(this.serverSettings.oidcConfig.defaultLibraries);

        this.settingsForm.addControl('authority', new FormControl(this.serverSettings.oidcConfig.authority, [], [this.authorityValidator()]));
        this.settingsForm.addControl('clientId', new FormControl(this.serverSettings.oidcConfig.clientId, [this.requiredIf('authority')]));
        this.settingsForm.addControl('secret', new FormControl(this.serverSettings.oidcConfig.secret, [this.requiredIf('authority')]));
        this.settingsForm.addControl('provisionAccounts', new FormControl(this.serverSettings.oidcConfig.provisionAccounts, []));
        this.settingsForm.addControl('requireVerifiedEmail', new FormControl(this.serverSettings.oidcConfig.requireVerifiedEmail, []));
        this.settingsForm.addControl('syncUserSettings', new FormControl(this.serverSettings.oidcConfig.syncUserSettings, []));
        this.settingsForm.addControl('rolesPrefix', new FormControl(this.serverSettings.oidcConfig.rolesPrefix, []));
        this.settingsForm.addControl('rolesClaim', new FormControl(this.serverSettings.oidcConfig.rolesClaim, []));
        this.settingsForm.addControl('autoLogin', new FormControl(this.serverSettings.oidcConfig.autoLogin, []));
        this.settingsForm.addControl('disablePasswordAuthentication', new FormControl(this.serverSettings.oidcConfig.disablePasswordAuthentication, []));
        this.settingsForm.addControl('providerName', new FormControl(this.serverSettings.oidcConfig.providerName, []));
        this.settingsForm.addControl("defaultAgeRestriction", new FormControl(this.serverSettings.oidcConfig.defaultAgeRestriction, []));
        this.settingsForm.addControl('defaultIncludeUnknowns', new FormControl(this.serverSettings.oidcConfig.defaultIncludeUnknowns, []));
        this.settingsForm.addControl('customScopes', new FormControl(this.serverSettings.oidcConfig.customScopes.join(","), []))
        this.cdRef.markForCheck();

        this.settingsForm.valueChanges.pipe(
          debounceTime(300),
          distinctUntilChanged(),
          takeUntilDestroyed(this.destroyRef),
          filter(() => {
            // Do not auto save when provider settings have changed
            const settings: OidcConfig = this.settingsForm.getRawValue();
            return settings.authority == this.oidcSettings()?.authority && settings.clientId == this.oidcSettings()?.clientId;
          }),
          tap(() => this.save())
        ).subscribe();
      }
    });
  }

  updateRoles(roles: string[]) {
    this.selectedRoles.set(roles);
    this.save();
  }

  updateLibraries(libraries: Library[]) {
    this.selectedLibraries.set(libraries.map(l => l.id));
    this.save();
  }

  save(showConfirmation: boolean = false) {
    if (!this.settingsForm.valid || !this.serverSettings || !this.oidcSettings) return;

    const data = this.settingsForm.getRawValue();
    const newSettings = Object.assign({}, this.serverSettings);
    newSettings.oidcConfig = data as OidcConfig;
    newSettings.oidcConfig.defaultAgeRestriction = parseInt(newSettings.oidcConfig.defaultAgeRestriction + '', 10) as AgeRating;
    newSettings.oidcConfig.defaultRoles = this.selectedRoles();
    newSettings.oidcConfig.defaultLibraries = this.selectedLibraries();
    newSettings.oidcConfig.customScopes = (data.customScopes as string)
      .split(',').map((item: string) => item.trim())
      .filter((scope: string) => scope.length > 0);

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

  breakString(s: string) {
    if (s) {
      return s.split(',');
    }

    return [];
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
