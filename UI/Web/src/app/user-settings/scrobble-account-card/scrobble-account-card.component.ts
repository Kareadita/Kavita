import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {UserScrobbleProvider} from '../../_services/scrobbling.service';
import {ScrobbleProviderNamePipe} from '../../_pipes/scrobble-provider-name.pipe';
import {SettingsTabId} from '../../sidenav/preference-nav/preference-nav.component';
import {
  ScrobbleProviderImageComponent
} from '../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {AccountService} from "../../_services/account.service";

@Component({
  selector: 'app-scrobble-account-card',
  templateUrl: './scrobble-account-card.component.html',
  styleUrls: ['./scrobble-account-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ScrobbleProviderNamePipe, ScrobbleProviderImageComponent, NgbTooltip],
})
export class ScrobbleAccountCardComponent {
  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);

  provider = input.required<UserScrobbleProvider>();

  isConnected = computed(() => {
    const token = this.provider().authenticationToken;
    return token != null && token.length > 0;
  });

  hasExpired = computed(() => {
    const until = this.provider().validUntilUtc;
    if (!until) return false;
    return new Date(until) < new Date();
  });

  username = computed(() => this.provider().userName ?? '');

  isScrobbleDisabled = computed(() => {
    return !this.accountService.currentUser()?.preferences.aniListScrobblingEnabled;
  })


  goToScrobbling() {
    this.router.navigate(['/settings'], {fragment: SettingsTabId.Account});
  }
}
