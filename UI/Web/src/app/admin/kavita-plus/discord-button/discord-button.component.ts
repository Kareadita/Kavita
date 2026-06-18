import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {APP_BASE_HREF, NgOptimizedImage} from "@angular/common";
import {OAuthUpstream} from "../../../_models/kavitaplus/oauth-upstream";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-discord-button',
  imports: [
    NgOptimizedImage
  ],
  templateUrl: './discord-button.component.html',
  styleUrl: './discord-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.block]': 'block()',
  },
})
export class DiscordButtonComponent {
  private readonly accountService = inject(AccountService);
  private readonly baseUrl = inject(APP_BASE_HREF);

  label = input<string>('');
  /** When true, the button fills the full width of its container (useful on mobile) */
  block = input<boolean>(false);

  href = computed(() => {
    const apiKey = this.accountService.currentUserGenericApiKey();
    if (!apiKey) return null;

    return this.baseUrl + `api/oauth/start?upstream=${OAuthUpstream.Discord}&apiKey=${apiKey}`;
  });
}
