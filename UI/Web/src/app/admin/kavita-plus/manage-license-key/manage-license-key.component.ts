import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {EditLicenseKeyComponent, LicenseFormEvent} from "../edit-license-key/edit-license-key.component";
import {LicenseService} from "../../../_services/license.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-manage-license-key',
  imports: [
    TranslocoDirective,
    EditLicenseKeyComponent
  ],
  templateUrl: './manage-license-key.component.html',
  styleUrl: './manage-license-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageLicenseKeyComponent {

  protected readonly licenseService = inject(LicenseService);
  protected readonly confirmService = inject(ConfirmService);
  protected readonly toastr = inject(ToastrService);

  protected readonly formIsValid = signal<boolean>(false);

  private formData: LicenseFormEvent | null = null;

  updateFormData(data: LicenseFormEvent) {
    this.formData = data;
    this.formIsValid.set(data.isValid);
  }


  async deleteLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-delete-key'))) {
      return;
    }

    this.licenseService.deleteLicense().subscribe(() => {
      // TODO: (the parent component should update due to the license logic changing)
    });
  }

  async resetLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-reset-key'))) {
      return;
    }

    if (!this.formData) return;

    this.licenseService.resetLicense(this.formData.licenseKey.trim(), this.formData.email.trim()).subscribe(() => {
      this.toastr.success(translate('toasts.k+-reset-key-success'));
    });
  }

}
