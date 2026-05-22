import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {
  ScrobbleAccountCardComponent
} from "../../../user-settings/scrobble-account-card/scrobble-account-card.component";
import {ScrobblingService, UserScrobbleProvider} from "../../../_services/scrobbling.service";
import {BannerComponent} from "../../../shared/_components/banner/banner.component";
import {Router} from "@angular/router";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {ToastrService} from "ngx-toastr";
import {LicenseService} from "../../../_services/license.service";

@Component({
  selector: 'app-kavita-plus-connect-providers',
  imports: [
    TranslocoDirective,
    ScrobbleAccountCardComponent,
    BannerComponent
  ],
  templateUrl: './kavita-plus-connect-providers.component.html',
  styleUrl: './kavita-plus-connect-providers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusConnectProvidersComponent {

  private readonly scrobblingService = inject(ScrobblingService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly licenseService = inject(LicenseService);

  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  backfillAndRedirect() {
    // Validate there are scrobble providers hooked up, if not, just redirect
    this.scrobblingService.getScrobbleProviders().subscribe(providers => {
      const enabledProviders = providers.filter(p => p.authenticationToken);
      if (enabledProviders.length > 0) {
        this.scrobblingService.triggerScrobbleEventGeneration().subscribe(res => {
          if (res) {
            this.toastr.info(translate('toasts.scrobble-gen-init'));
          }
          this.refresh();
        });
      } else {
        this.refresh();
      }
    })
  }

  skip() {
    // Call hasAnyLicense to ensure we don't show the wizard
    this.refresh();
  }

  private refresh() {
    this.licenseService.hasAnyLicense().subscribe(hasLicense => {
      this.router.navigate(['/settings'], {fragment: SettingsTabId.KavitaPlusLicense});
    });
  }

  protected readonly WikiLink = WikiLink;
  protected readonly SettingsTabId = SettingsTabId;
}
