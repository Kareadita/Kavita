import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {LicenseInfoPanelComponent} from '../license-info-panel/license-info-panel.component';
import {editModal} from "../../../_models/modal/modal-options";
import {ModalService} from "../../../_services/modal.service";
import {
  ManageLicenseKeyModalComponent
} from "../../_modals/manage-license-key-modal/manage-license-key-modal.component";

@Component({
  selector: 'app-license-dashboard',
  imports: [
    TranslocoDirective,
    LicenseInfoPanelComponent,
  ],
  templateUrl: './license-dashboard.component.html',
  styleUrl: './license-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseDashboardComponent {

  private readonly modalService = inject(ModalService);

  licenseInfo = input.required<LicenseInfo | null>();

  forceCheckLicense() {}

  openEditLicenseModal() {
    const ref = this.modalService.open(ManageLicenseKeyModalComponent, editModal());
    ref.setInput('licenseInfo', this.licenseInfo()!);
    ref.closed.subscribe(res => {

    });
  }



  protected readonly WikiLink = WikiLink;
}
