import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {LicenseInfoPanelComponent} from '../license-info-panel/license-info-panel.component';
import {editModal} from "../../../_models/modal/modal-options";
import {ModalService} from "../../../_services/modal.service";
import {
  ManageLicenseKeyModalComponent
} from "../../_modals/manage-license-key-modal/manage-license-key-modal.component";
import {LicenseService} from "../../../_services/license.service";
import {DiscordButtonComponent} from "../discord-button/discord-button.component";
import {ScrobbleHealthComponent} from '../scrobble-health/scrobble-health.component';

@Component({
  selector: 'app-license-dashboard',
  imports: [
    TranslocoDirective,
    LicenseInfoPanelComponent,
    DiscordButtonComponent,
    ScrobbleHealthComponent,
  ],
  templateUrl: './license-dashboard.component.html',
  styleUrl: './license-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseDashboardComponent {

  private readonly modalService = inject(ModalService);
  protected readonly licenseService = inject(LicenseService);

  forceCheckLicense() {
    this.licenseService.getLicenseInfo(true).subscribe();
  }

  openEditLicenseModal() {
    const ref = this.modalService.open(ManageLicenseKeyModalComponent, editModal());
    ref.closed.subscribe();
  }

  protected readonly WikiLink = WikiLink;
}
