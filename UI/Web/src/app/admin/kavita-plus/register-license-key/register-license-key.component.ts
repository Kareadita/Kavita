import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {LicenseService} from "../../../_services/license.service";
import {EditLicenseKeyComponent, LicenseFormEvent} from "../edit-license-key/edit-license-key.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {
  KavitaPlusRegistrationErrorCode
} from '../../../_models/kavitaplus/registration/kavita-plus-registration-error-code';
import {KavitaPlusRegistrationErrorCodePipe} from '../../../_pipes/registration-error-code.pipe';

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
    this.licenseService.registerLicense(this.formData.licenseKey, this.formData.email, this.formData.discordId ?? undefined).subscribe(result => {
      this.isLoading.set(false);
      if (!result.success) {
        this.errorCode.set(result.errorCode ?? KavitaPlusRegistrationErrorCode.InternalError);
      }
    });
  }
}
