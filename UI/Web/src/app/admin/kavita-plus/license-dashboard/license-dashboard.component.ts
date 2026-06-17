import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
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
import {ScrobblingService} from "../../../_services/scrobbling.service";
import {LicenseApiStatsComponent} from "../license-api-stats/license-api-stats.component";
import {ExpiredLicenseApiStatsComponent} from "../expired-license-api-stats/expired-license-api-stats.component";
import {UserScrobbleProvider} from "../../../_models/kavitaplus/scrobble-providers/user-scrobble-provider";
import {KavitaPlusSubscriptionState} from "../../../_models/kavitaplus/license-info";
import {filter, map, switchMap, tap} from "rxjs";
import {EVENTS, MessageHubService} from "../../../_services/message-hub.service";
import {ScrobbleProviderUpdatedEvent} from "../../../_models/events/scrobble-provider-updated-event";
import {ActivatedRoute} from "@angular/router";
import {take} from "rxjs/operators";

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
export class LicenseDashboardComponent implements OnInit {

  private readonly modalService = inject(ModalService);
  private readonly scrobblingService = inject(ScrobblingService);
  protected readonly licenseService = inject(LicenseService);
  private readonly messageHub = inject(MessageHubService);
  private readonly route = inject(ActivatedRoute);

  isLoadingDiscordInfoInBackground = signal(false);
  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  ngOnInit() {
    this.route.queryParamMap.pipe(
      map(m => m.get('loading')),
      take(1),
      filter(loading => loading === 'true'),
      tap(() => this.isLoadingDiscordInfoInBackground.set(true))
    ).subscribe();


    this.messageHub.messages$.pipe(
      filter(msg => msg.event === EVENTS.LicenseInfoUpdate),
      switchMap(() => this.licenseService.getLicenseInfo()),
      tap(() => this.isLoadingDiscordInfoInBackground.set(false))
    ).subscribe();
  }

  forceCheckLicense() {
    this.licenseService.getLicenseInfo(true).subscribe();
  }

  openEditLicenseModal() {
    const ref = this.modalService.open(ManageLicenseKeyModalComponent, editModal());
    ref.closed.subscribe();
  }

  protected readonly WikiLink = WikiLink;
  protected readonly KavitaPlusSubscriptionState = KavitaPlusSubscriptionState;
}
