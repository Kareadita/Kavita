import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {DatePipe, NgClass, UpperCasePipe} from '@angular/common';
import {NgCircleProgressModule} from 'ng-circle-progress';
import {KavitaPlusSubscriptionState, LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {environment} from '../../../../environments/environment';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {VersionService} from '../../../_services/version.service';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusSubscriptionStatusPipe} from '../../../_pipes/kavita-plus-subscription-status.pipe';
import {KavitaPlusBillingIntervalPipe} from '../../../_pipes/kavita-plus-billing-interval.pipe';

@Component({
  selector: 'app-license-info-panel',
  templateUrl: './license-info-panel.component.html',
  styleUrl: './license-info-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, NgCircleProgressModule, UtcToLocalTimePipe, UpperCasePipe, DatePipe, TranslocoDirective, KavitaPlusSubscriptionStatusPipe, KavitaPlusBillingIntervalPipe],
  host: {'[attr.color]': 'status()'},
})
export class LicenseInfoPanelComponent {

  private readonly versionService = inject(VersionService);

  licenseInfo = input.required<LicenseInfo | null>();
  editKey = output<void>();

  readonly status = computed((): 'active' | 'paused' | 'cancelled' => {
    const info = this.licenseInfo();
    if (!info) return 'paused';

    if (info.isCancelled) return 'cancelled';
    if (info.isActive) return 'active';
    return 'paused';
  });


  readonly activeVersion = this.versionService.currentVersion;

  readonly daysRemaining = computed((): number => {
    const expDate = this.licenseInfo()?.expirationDate;
    if (!expDate) return 0;
    const diff = Math.ceil((new Date(expDate).getTime() - Date.now()) / 86_400_000);
    return Math.max(0, diff);
  });

  readonly daysRemainingPercent = computed((): number =>
    Math.min(100, Math.round((this.daysRemaining() / 30) * 100))
  );

  readonly manageLink = computed((): string => {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email) return environment.manageLink;
    return environment.manageLink + '?prefilled_email=' + encodeURIComponent(email);
  });

  readonly daysAgo = computed((): number => {
    const exp = this.licenseInfo()?.expirationDate;
    if (!exp) return 0;
    return Math.max(0, Math.floor((Date.now() - new Date(exp).getTime()) / 86_400_000));
  });

  readonly formattedPrice = computed((): string | null => {
    const amount = this.licenseInfo()?.priceAmount;
    const currency = this.licenseInfo()?.priceCurrency;
    if (amount == null || !currency) return null;
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency.toUpperCase(),
    }).format(amount / 100);
  });

  // SVG attributes cannot resolve CSS custom properties - map status to literal color values.
  readonly ringColor = computed((): string => {
    switch (this.status()) {
      case 'active':    return '#4ac694';
      case 'cancelled': return '#dc3545';
      case 'paused':    return '#ffc107';
    }
  });
  protected readonly KavitaPlusSubscriptionStatusPipe = KavitaPlusSubscriptionStatusPipe;
  protected readonly KavitaPlusSubscriptionState = KavitaPlusSubscriptionState;
}
