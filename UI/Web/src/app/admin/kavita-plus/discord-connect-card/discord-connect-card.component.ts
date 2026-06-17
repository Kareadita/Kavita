import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {DiscordButtonComponent} from '../discord-button/discord-button.component';
import {BreakpointService} from '../../../_services/breakpoint.service';
import {OAuthUpstream} from "../../../_models/kavitaplus/oauth-upstream";
import {APP_BASE_HREF} from "@angular/common";
import {AccountService} from "../../../_services/account.service";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-discord-connect-card',
  imports: [TranslocoDirective, DiscordButtonComponent, SafeUrlPipe, NgbTooltip],
  templateUrl: './discord-connect-card.component.html',
  styleUrl: './discord-connect-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscordConnectCardComponent {

  protected readonly breakpointService = inject(BreakpointService);
  private readonly baseUrl = inject(APP_BASE_HREF);
  private readonly accountService = inject(AccountService);

  licenseInfo = input<LicenseInfo | null>(null);
  loadingDiscordInBackground = input<boolean>(false);

  readonly isConnected = computed((): boolean => this.licenseInfo()?.hasDiscordSet ?? false);
  readonly discordUsername = computed((): string | null => this.licenseInfo()?.discordUsername ?? null);
  readonly discordId = computed((): string | null => this.licenseInfo()?.discordId ?? null);

  discordOAuthFlow = computed(() => {
    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) return null;

    return this.baseUrl + `api/oauth/start?upstream=${OAuthUpstream.Discord}&apiKey=${apiKey}`;
  });
}
