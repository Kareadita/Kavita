import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {KavitaPlusSubscriptionStatusPipe} from '../../../_pipes/kavita-plus-subscription-status.pipe';
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";

@Component({
  selector: 'app-expired-license-info-card',
  templateUrl: './expired-license-info-card.component.html',
  styleUrl: './expired-license-info-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, KavitaPlusSubscriptionStatusPipe, UtcToLocalTimePipe],
})
export class ExpiredLicenseInfoCardComponent {
  protected readonly licenseService = inject(LicenseService);
}
