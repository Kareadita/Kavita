import {ChangeDetectionStrategy, Component, inject, input, signal} from '@angular/core';
import {EditLicenseKeyComponent, LicenseFormEvent} from "../../kavita-plus/edit-license-key/edit-license-key.component";
import {LicenseService} from "../../../_services/license.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {ToastrService} from "ngx-toastr";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {LicenseInfo} from "../../../_models/kavitaplus/license-info";

@Component({
  selector: 'app-manage-license-key-modal',
  imports: [
    EditLicenseKeyComponent,
    TranslocoDirective
  ],
  templateUrl: './manage-license-key-modal.component.html',
  styleUrl: './manage-license-key-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageLicenseKeyModalComponent {

  protected readonly modal = inject(NgbActiveModal);
  protected readonly licenseService = inject(LicenseService);
  protected readonly confirmService = inject(ConfirmService);
  protected readonly toastr = inject(ToastrService);

  licenseInfo = input.required<LicenseInfo>();

  protected readonly formIsValid = signal<boolean>(false);

  private formData: LicenseFormEvent | null = null;

  updateFormData(data: LicenseFormEvent) {
    this.formData = data;
    this.formIsValid.set(data.isValid);
  }

// TODO: This needs to be ported still
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

  save() {
    // TODO
  }


}
