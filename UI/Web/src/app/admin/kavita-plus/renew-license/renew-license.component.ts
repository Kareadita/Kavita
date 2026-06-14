import {ChangeDetectionStrategy, Component, inject, output, signal} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {ManageLicenseModalScreen} from '../_modals/manage-license-modal/manage-license-modal-screen';

type BillingPeriod = 'monthly' | 'yearly';

@Component({
  selector: 'app-renew-license',
  imports: [TranslocoDirective],
  templateUrl: './renew-license.component.html',
  styleUrl: './renew-license.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RenewLicenseComponent implements ManageLicenseModalScreen {

  private readonly licenseService = inject(LicenseService);

  readonly back = output<void>();
  readonly dismiss = output<void>();
  /** Navigate to the change-license-email screen. */
  readonly changeEmail = output<void>();

  protected readonly licenseInfo = this.licenseService.licenseInfo;
  protected readonly billingPeriod = signal<BillingPeriod>('monthly');

  selectPeriod(period: BillingPeriod) {
    this.billingPeriod.set(period);
  }
}
