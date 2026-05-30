import {ChangeDetectionStrategy, Component, computed, inject, input, output, signal} from '@angular/core';
import {DatePipe, UpperCasePipe} from '@angular/common';
import {KavitaPlusBillingInterval, LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {environment} from '../../../../environments/environment';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {VersionService} from '../../../_services/version.service';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusSubscriptionStatusPipe} from '../../../_pipes/kavita-plus-subscription-status.pipe';
import {KavitaPlusBillingIntervalPipe} from '../../../_pipes/kavita-plus-billing-interval.pipe';
import {MemberService} from "../../../_services/member.service";

@Component({
  selector: 'app-license-info-panel',
  templateUrl: './license-info-panel.component.html',
  styleUrl: './license-info-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [UtcToLocalTimePipe, UpperCasePipe, DatePipe, TranslocoDirective, KavitaPlusSubscriptionStatusPipe, KavitaPlusBillingIntervalPipe],
  host: { '[attr.data-status]': 'status()' }
})
export class LicenseInfoPanelComponent {

  private readonly versionService = inject(VersionService);
  private readonly userService = inject(MemberService);

  licenseInfo = input.required<LicenseInfo | null>();
  editLicense = output<void>();
  /** Triggers a license validation and forced reload of license info */
  check = output<void>();

  usersCount = signal<number>(0);

  readonly status = computed((): 'active' | 'paused' | 'cancelled' => {
    const info = this.licenseInfo();
    if (!info) return 'paused';

    if (info.isCancelled) return 'cancelled';
    if (info.isActive) return 'active';
    return 'paused';
  });

  readonly activeVersion = this.versionService.currentVersion;

  readonly daysRemaining = computed((): number => {
    return this.licenseInfo()?.daysRemaining ?? 0;
  });

  readonly daysRemainingPercent = computed((): number =>
    Math.min(100, Math.round((this.daysRemaining() / 30) * 100))
  );

  readonly manageLink = computed((): string => {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email) return environment.manageLink;
    return environment.manageLink + '?prefilled_email=' + encodeURIComponent(email);
  });

  readonly isExpired = computed((): boolean => {
    return this.licenseInfo()?.isExpired ?? true;

  });

  readonly daysUntilExpiry = computed((): number => {
    return this.licenseInfo()?.daysUntilExpiry ?? 0;
  });

  readonly daysUntilExpiryPercent = computed((): number => {
    const days = this.daysUntilExpiry();
    const interval = this.licenseInfo()?.billingInterval;
    const totalDays = interval === KavitaPlusBillingInterval.Year ? 365
      : interval === KavitaPlusBillingInterval.Week ? 7
      : interval === KavitaPlusBillingInterval.Day  ? 1
      : 30;
    return Math.min(100, Math.round((days / totalDays) * 100));
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

  constructor() {
    this.userService.getMembers(false).subscribe(members => {
      this.usersCount.set(members.length);
    });
  }

}
