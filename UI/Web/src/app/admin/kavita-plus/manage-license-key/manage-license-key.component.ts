import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {EditLicenseKeyComponent, LicenseFormEvent} from "../edit-license-key/edit-license-key.component";
import {LicenseService} from "../../../_services/license.service";

/**
 * This is the extra buttons (delete, reset)
 */
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

  protected readonly formIsValid = signal<boolean>(false);

  updateFormData(data: LicenseFormEvent) {
    console.log(data)
    this.formIsValid.set(data.isValid);
  }

}
