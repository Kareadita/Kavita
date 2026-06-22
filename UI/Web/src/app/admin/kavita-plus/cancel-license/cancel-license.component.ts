import {ChangeDetectionStrategy, Component, computed, inject, output, signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {TranslocoDirective, TranslocoService} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {MemberService} from '../../../_services/member.service';
import {KavitaPlusSubscriptionState} from '../../../_models/kavitaplus/license-info';
import {KavitaPlusSubscriptionStatusPipe} from '../../../_pipes/kavita-plus-subscription-status.pipe';
import {KavitaPlusBillingIntervalPipe} from '../../../_pipes/kavita-plus-billing-interval.pipe';
import {ManageLicenseModalScreen} from '../_modals/manage-license-modal/manage-license-modal-screen';

@Component({
  selector: 'app-cancel-license',
  imports: [TranslocoDirective, FormsModule, KavitaPlusSubscriptionStatusPipe, KavitaPlusBillingIntervalPipe],
  templateUrl: './cancel-license.component.html',
  styleUrl: './cancel-license.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CancelLicenseComponent implements ManageLicenseModalScreen {

  private readonly licenseService = inject(LicenseService);
  private readonly memberService = inject(MemberService);
  private readonly toastr = inject(ToastrService);
  private readonly translocoService = inject(TranslocoService);

  readonly back = output<void>();
  readonly dismiss = output<void>();

  protected readonly licenseInfo = this.licenseService.licenseInfo;
  protected readonly usersCount = signal<number>(0);
  protected readonly comment = signal<string>('');
  protected readonly isCancelling = signal<boolean>(false);

  protected readonly canConfirm = computed((): boolean => !!this.licenseInfo()?.registeredEmail);

  protected readonly statusToken = computed((): 'active' | 'cancelling' | 'paused' | 'expired' => {
    switch (this.licenseInfo()?.state) {
      case KavitaPlusSubscriptionState.Active:
        return 'active';
      case KavitaPlusSubscriptionState.Cancelling:
        return 'cancelling';
      case KavitaPlusSubscriptionState.Paused:
        return 'paused';
      default:
        return 'expired';
    }
  });

  protected readonly formattedPrice = computed((): string | null => {
    const amount = this.licenseInfo()?.priceAmount;
    const currency = this.licenseInfo()?.priceCurrency;
    if (amount == null || !currency) return null;

    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency.toUpperCase(),
    }).format(amount / 100);
  });

  constructor() {
    this.memberService.getMembers(false).subscribe(members => {
      this.usersCount.set(members.length);
    });
  }

  cancelLicense() {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email) return;

    this.isCancelling.set(true);
    this.licenseService.cancelLicense(email, this.comment().trim() || undefined)
      .subscribe({
        next: () => {
          this.toastr.success(this.translocoService.translate('cancel-license.cancelled-success'));
          this.dismiss.emit();
        },
        error: () => {
          this.toastr.error(this.translocoService.translate('cancel-license.cancelled-error'));
          this.isCancelling.set(false);
        }
      });
  }

}
