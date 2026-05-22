import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {
  ScrobbleAccountCardComponent
} from "../../../user-settings/scrobble-account-card/scrobble-account-card.component";
import {ScrobblingService, UserScrobbleProvider} from "../../../_services/scrobbling.service";
import {DiscordButtonComponent} from "../discord-button/discord-button.component";

@Component({
  selector: 'app-kavita-plus-connect-providers',
  imports: [
    TranslocoDirective,
    ScrobbleAccountCardComponent,
    DiscordButtonComponent
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
}
