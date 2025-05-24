import {ChangeDetectorRef, Component, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ServerSettings} from "../_models/server-settings";
import {
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


        // TODO: Validator for authority, /.well-known/openid-configuration endpoint must be reachable
        this.settingsForm.addControl('authority', new FormControl(this.oidcSettings.authority, []));
        this.settingsForm.addControl('clientId', new FormControl(this.oidcSettings.clientId, [this.requiredIf('authority')]));
        this.settingsForm.addControl('provisionAccounts', new FormControl(this.oidcSettings.provisionAccounts, []));
        this.settingsForm.addControl('requireVerifiedEmail', new FormControl(this.oidcSettings.requireVerifiedEmail, []));
        this.settingsForm.addControl('provisionUserSettings', new FormControl(this.oidcSettings.provisionUserSettings, []));
        this.settingsForm.addControl('autoLogin', new FormControl(this.oidcSettings.autoLogin, []));
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
