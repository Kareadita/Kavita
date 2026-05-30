import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {LicenseInfoPanelComponent} from '../license-info-panel/license-info-panel.component';
import {editModal} from "../../../_models/modal/modal-options";
import {ModalService} from "../../../_services/modal.service";
import {
  ManageLicenseKeyModalComponent
} from "../../_modals/manage-license-key-modal/manage-license-key-modal.component";
import {LicenseService} from "../../../_services/license.service";
import {DiscordConnectCardComponent} from "../discord-connect-card/discord-connect-card.component";
import {ScrobbleHealthComponent} from '../scrobble-health/scrobble-health.component';
import {ExpiredLicenseInfoCardComponent} from '../expired-license-info-card/expired-license-info-card.component';
import {
  ScrobbleAccountCardComponent
} from "../../../user-settings/scrobble-account-card/scrobble-account-card.component";
import {ScrobblingService, UserScrobbleProvider} from "../../../_services/scrobbling.service";
import {LicenseApiStatsComponent} from "../license-api-stats/license-api-stats.component";
import {ExpiredLicenseApiStatsComponent} from "../expired-license-api-stats/expired-license-api-stats.component";

@Component({
  selector: 'app-license-dashboard',
  imports: [
    TranslocoDirective,
    LicenseInfoPanelComponent,
    DiscordConnectCardComponent,
    ScrobbleHealthComponent,
    ExpiredLicenseInfoCardComponent,
    ScrobbleAccountCardComponent,
    LicenseApiStatsComponent,
    ExpiredLicenseApiStatsComponent,
  ],
  templateUrl: './license-dashboard.component.html',
  styleUrl: './license-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseDashboardComponent {

  private readonly modalService = inject(ModalService);
  private readonly scrobblingService = inject(ScrobblingService);
  protected readonly licenseService = inject(LicenseService);

  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  forceCheckLicense() {
    this.licenseService.getLicenseInfo(true).subscribe();
  }

  openEditLicenseModal() {
    const ref = this.modalService.open(ManageLicenseKeyModalComponent, editModal());
    ref.closed.subscribe();
  }

  protected readonly WikiLink = WikiLink;
}
