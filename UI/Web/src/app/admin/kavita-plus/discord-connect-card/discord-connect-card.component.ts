import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {DiscordButtonComponent} from '../discord-button/discord-button.component';
import {BreakpointService} from '../../../_services/breakpoint.service';

@Component({
  selector: 'app-discord-connect-card',
  imports: [TranslocoDirective, DiscordButtonComponent],
  templateUrl: './discord-connect-card.component.html',
  styleUrl: './discord-connect-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscordConnectCardComponent {

  protected readonly breakpointService = inject(BreakpointService);

  licenseInfo = input<LicenseInfo | null>(null);

  readonly isConnected = computed((): boolean => this.licenseInfo()?.hasDiscordSet ?? false);
  readonly discordId = computed((): string | null => this.licenseInfo()?.discordId ?? null);
}
