import {ChangeDetectionStrategy, Component, inject, output, signal} from '@angular/core';
import {LicenseService} from "../../../_services/license.service";
import {EditLicenseKeyComponent, LicenseFormEvent} from "../edit-license-key/edit-license-key.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {
  KavitaPlusRegistrationErrorCode
} from '../../../_models/kavitaplus/registration/kavita-plus-registration-error-code';
import {KavitaPlusRegistrationErrorCodePipe} from '../../../_pipes/registration-error-code.pipe';
import {ConfirmService} from "../../../shared/confirm.service";

@Component({
  selector: 'app-register-license-key',
  imports: [
    EditLicenseKeyComponent,
    TranslocoDirective,
    KavitaPlusRegistrationErrorCodePipe,
  ],
  templateUrl: './register-license-key.component.html',
  styleUrl: './register-license-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterLicenseKeyComponent {
  protected readonly licenseService = inject(LicenseService);
  protected readonly confirmService = inject(ConfirmService);

  /*On save, emits if license is active or not */
  saved = output<boolean>();


  protected readonly formIsValid = signal<boolean>(false);
  protected readonly isLoading = signal(false);
  protected readonly errorCode = signal<KavitaPlusRegistrationErrorCode | null>(null);

  private formData: LicenseFormEvent | null = null;

  updateFormData(data: LicenseFormEvent) {
    this.formData = data;
    this.formIsValid.set(data.isValid);
  }

  save() {
    if (!this.formData) return;
    this.isLoading.set(true);
    this.errorCode.set(null);
    this.licenseService.registerLicense(this.formData.licenseKey, this.formData.email, this.formData.discordId ?? undefined).subscribe(async result => {
      this.isLoading.set(false);
      if (!result.success) {
        if (result.errorCode === KavitaPlusRegistrationErrorCode.AlreadyRegistered) {
          const answer = await this.confirmService.confirm(translate('license.k+-license-overwrite'), {
            _type: 'confirm',
            content: translate('license.k+-license-overwrite'),
            disableEscape: false,
            header: translate('license.k+-already-registered-header'),
            buttons: [
              {text: translate('license.overwrite'), type: 'primary'},
              {text: translate('license.cancel'), type: 'secondary'},
            ]
          });
          if (answer) {
            this.forceSave();
            return;
          }
        }

        this.errorCode.set(result.errorCode ?? KavitaPlusRegistrationErrorCode.InternalError);
        return;
      }

      // When success flow, inform the parent and pass if sub was active or not
      this.saved.emit(result.isSubscriptionActive);
    });
  }

  forceSave() {
    this.licenseService.resetLicense(this.formData!.licenseKey, this.formData!.email)
      .subscribe(_ => this.save());
  }
}
