import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, effect, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
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

@Component({
  selector: 'app-manage-open-idconnect',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    SettingSwitchComponent,
    AgeRatingPipe,
    LibrarySelectorComponent,
    RoleSelectorComponent
  ],
  templateUrl: './manage-open-idconnect.component.html',
  styleUrl: './manage-open-idconnect.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageOpenIDConnectComponent implements OnInit {

  serverSettings!: ServerSettings;
  oidcSettings = signal<OidcConfig | undefined>(undefined);
  settingsForm: FormGroup = new FormGroup({});

  ageRatings = signal<AgeRatingDto[]>([]);
  selectedLibraries = signal<number[]>([]);
  selectedRoles = signal<string[]>([]);

  constructor(
    private settingsService: SettingsService,
    private cdRef: ChangeDetectorRef,
    private destroyRef: DestroyRef,
    private metadataService: MetadataService,
    private toastr: ToastrService,
  ) {
  }

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
        this.settingsForm.addControl('provisionAccounts', new FormControl(this.serverSettings.oidcConfig.provisionAccounts, []));
        this.settingsForm.addControl('requireVerifiedEmail', new FormControl(this.serverSettings.oidcConfig.requireVerifiedEmail, []));
        this.settingsForm.addControl('syncUserSettings', new FormControl(this.serverSettings.oidcConfig.syncUserSettings, []));
        this.settingsForm.addControl('autoLogin', new FormControl(this.serverSettings.oidcConfig.autoLogin, []));
        this.settingsForm.addControl('disablePasswordAuthentication', new FormControl(this.serverSettings.oidcConfig.disablePasswordAuthentication, []));
        this.settingsForm.addControl('providerName', new FormControl(this.serverSettings.oidcConfig.providerName, []));
        this.settingsForm.addControl("defaultAgeRestriction", new FormControl(this.serverSettings.oidcConfig.defaultAgeRestriction, []));
        this.settingsForm.addControl('defaultIncludeUnknowns', new FormControl(this.serverSettings.oidcConfig.defaultIncludeUnknowns, []));
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

  save() {
    if (!this.settingsForm.valid || !this.serverSettings || !this.oidcSettings) return;

    const data = this.settingsForm.getRawValue();
    const newSettings = Object.assign({}, this.serverSettings);
    newSettings.oidcConfig = data as OidcConfig;
    newSettings.oidcConfig.defaultAgeRestriction = parseInt(newSettings.oidcConfig.defaultAgeRestriction as unknown as string, 10) as AgeRating;
    newSettings.oidcConfig.defaultRoles = this.selectedRoles();
    newSettings.oidcConfig.defaultLibraries = this.selectedLibraries();

    this.settingsService.updateServerSettings(newSettings).subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings.set(data.oidcConfig);
        this.cdRef.markForCheck();
      },
      error: error => {
        console.error(error);
        this.toastr.error("errors.generic")
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

      if (uri.endsWith('/')) {
        uri = uri.substring(0, uri.length - 1);
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
