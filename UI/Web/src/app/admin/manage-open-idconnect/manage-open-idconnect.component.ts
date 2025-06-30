import {ChangeDetectorRef, Component, OnInit} from '@angular/core';
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
import {map, of} from "rxjs";

@Component({
  selector: 'app-manage-open-idconnect',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    SettingSwitchComponent
  ],
  templateUrl: './manage-open-idconnect.component.html',
  styleUrl: './manage-open-idconnect.component.scss'
})
export class ManageOpenIDConnectComponent implements OnInit {

  serverSettings!: ServerSettings;
  oidcSettings!: OidcConfig;
  settingsForm: FormGroup = new FormGroup({});

  constructor(
    private settingsService: SettingsService,
    private cdRef: ChangeDetectorRef,
  ) {
  }

  ngOnInit(): void {
    this.settingsService.getServerSettings().subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings = this.serverSettings.oidcConfig;

        this.settingsForm.addControl('authority', new FormControl(this.oidcSettings.authority, [], [this.authorityValidator()]));
        this.settingsForm.addControl('clientId', new FormControl(this.oidcSettings.clientId, [this.requiredIf('authority')]));
        this.settingsForm.addControl('provisionAccounts', new FormControl(this.oidcSettings.provisionAccounts, []));
        this.settingsForm.addControl('requireVerifiedEmail', new FormControl(this.oidcSettings.requireVerifiedEmail, []));
        this.settingsForm.addControl('provisionUserSettings', new FormControl(this.oidcSettings.provisionUserSettings, []));
        this.settingsForm.addControl('autoLogin', new FormControl(this.oidcSettings.autoLogin, []));
        this.settingsForm.addControl('disablePasswordAuthentication', new FormControl(this.oidcSettings.disablePasswordAuthentication, []));
        this.cdRef.markForCheck();
      }
    })
  }

  save() {
    const data = this.settingsForm.getRawValue();
    const newSettings = Object.assign({}, this.serverSettings);
    newSettings.oidcConfig = data as OidcConfig;

    this.settingsService.updateServerSettings(newSettings).subscribe({
      next: data => {
        this.serverSettings = data;
        this.oidcSettings = data.oidcConfig;
        this.cdRef.markForCheck();
      },
      error: error => {
        console.error(error);
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
