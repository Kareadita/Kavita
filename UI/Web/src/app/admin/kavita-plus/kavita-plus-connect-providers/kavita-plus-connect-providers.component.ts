import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {
  ScrobbleAccountCardComponent
} from "../../../user-settings/scrobble-account-card/scrobble-account-card.component";
import {ScrobblingService, UserScrobbleProvider} from "../../../_services/scrobbling.service";
import {BannerComponent} from "../../../shared/_components/banner/banner.component";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";

@Component({
  selector: 'app-kavita-plus-connect-providers',
  imports: [
    TranslocoDirective,
    ScrobbleAccountCardComponent,
    BannerComponent,
    RouterLink
  ],
  templateUrl: './kavita-plus-connect-providers.component.html',
  styleUrl: './kavita-plus-connect-providers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusConnectProvidersComponent {

  private readonly scrobblingService = inject(ScrobblingService);

  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }
  protected readonly WikiLink = WikiLink;
  protected readonly SettingsTabId = SettingsTabId;
}
