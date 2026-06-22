import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ScrobbleProviderNamePipe} from '../../_pipes/scrobble-provider-name.pipe';
import {SettingsTabId} from '../../sidenav/preference-nav/preference-nav.component';
import {
  ScrobbleProviderImageComponent
} from '../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {UserScrobbleProvider} from "../../_models/kavitaplus/scrobble-providers/user-scrobble-provider";

@Component({
  selector: 'app-scrobble-account-card',
  templateUrl: './scrobble-account-card.component.html',
  styleUrls: ['./scrobble-account-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ScrobbleProviderNamePipe, ScrobbleProviderImageComponent, NgbTooltip],
})
export class ScrobbleAccountCardComponent {

  private readonly router = inject(Router);

  provider = input.required<UserScrobbleProvider>();

  isConnected = computed(() => {
    const token = this.provider().authenticationToken;
    return token != null && token.length > 0;
  });

  hasExpired = computed(() => {
    const until = this.provider().validUntilUtc;
    if (!until || until === '0001-01-01T00:00:00') return false;

    return new Date(until) < new Date();
  });

  username = computed(() => this.provider().userName ?? '');


  goToScrobbling() {
    this.router.navigate(['/settings'], {fragment: SettingsTabId.ScrobbleSettings});
  }
}
