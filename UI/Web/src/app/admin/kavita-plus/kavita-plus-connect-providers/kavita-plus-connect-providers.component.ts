import {ChangeDetectionStrategy, Component, inject, output, signal} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {
  ScrobbleAccountCardComponent
} from "../../../user-settings/scrobble-account-card/scrobble-account-card.component";
import {ScrobblingService, UserScrobbleProvider} from "../../../_services/scrobbling.service";
import {BannerComponent} from "../../../shared/_components/banner/banner.component";
import {ToastrService} from "ngx-toastr";

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
  private readonly toastr = inject(ToastrService);

  done = output();

  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  backfillAndRedirect() {
    this.scrobblingService.getScrobbleProviders().subscribe(providers => {
      const enabledProviders = providers.filter(p => p.authenticationToken);
      if (enabledProviders.length > 0) {
        this.scrobblingService.triggerScrobbleEventGeneration().subscribe(res => {
          if (res) {
            this.toastr.info(translate('toasts.scrobble-gen-init'));
          }
          this.done.emit();
        });
      } else {
        this.done.emit();
      }
    });
  }

  skip() {
    this.done.emit();
  }

  protected readonly WikiLink = WikiLink;
}
