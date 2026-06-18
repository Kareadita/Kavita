import {ChangeDetectionStrategy, Component, computed, inject, output, signal} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
import {TranslocoDirective, TranslocoService} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {KavitaPlusBillingInterval} from '../../../_models/kavitaplus/license-info';
import {KavitaPlusProductInfo} from '../../../_models/kavitaplus/kavita-plus-product-info';
import {KavitaPlusBillingIntervalPipe} from '../../../_pipes/kavita-plus-billing-interval.pipe';
import {ManageLicenseModalScreen} from '../_modals/manage-license-modal/manage-license-modal-screen';

@Component({
  selector: 'app-renew-license',
  imports: [TranslocoDirective, KavitaPlusBillingIntervalPipe],
  templateUrl: './renew-license.component.html',
  styleUrl: './renew-license.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RenewLicenseComponent implements ManageLicenseModalScreen {

  private readonly licenseService = inject(LicenseService);
  private readonly toastr = inject(ToastrService);
  private readonly translocoService = inject(TranslocoService);

  readonly back = output<void>();
  readonly dismiss = output<void>();
  /** Navigate to the change-license-email screen. */
  readonly changeEmail = output<void>();

  protected readonly licenseInfo = this.licenseService.licenseInfo;
  protected readonly products = signal<KavitaPlusProductInfo[]>([]);
  protected readonly selectedInterval = signal<KavitaPlusBillingInterval>(KavitaPlusBillingInterval.Month);
  protected readonly isSending = signal<boolean>(false);
  /** Stripe Checkout (Pay Now) URL returned after a successful renew request. */
  protected readonly checkoutUrl = signal<string | null>(null);

  protected readonly monthlyProduct = computed((): KavitaPlusProductInfo | undefined =>
    this.products().find(p => p.billingInterval === KavitaPlusBillingInterval.Month));
  protected readonly yearlyProduct = computed((): KavitaPlusProductInfo | undefined =>
    this.products().find(p => p.billingInterval === KavitaPlusBillingInterval.Year));

  protected readonly selectedProduct = computed((): KavitaPlusProductInfo | undefined =>
    this.selectedInterval() === KavitaPlusBillingInterval.Year ? this.yearlyProduct() : this.monthlyProduct());

  protected readonly KavitaPlusBillingInterval = KavitaPlusBillingInterval;

  constructor() {
    this.licenseService.getProducts().subscribe(products => this.products.set(products));
  }

  formattedPrice(product: KavitaPlusProductInfo | undefined): string | null {
    if (!product || product.priceAmount == null || !product.priceCurrency) return null;

    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: product.priceCurrency.toUpperCase(),
    }).format(product.priceAmount / 100);
  }

  selectPeriod(interval: KavitaPlusBillingInterval) {
    this.selectedInterval.set(interval);
  }

  sendLink() {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email || !this.selectedProduct()) return;

    this.isSending.set(true);
    this.licenseService.renewLicense(email, this.selectedInterval())
      .subscribe({
        next: (checkoutUrl) => {
          this.isSending.set(false);
          this.checkoutUrl.set(checkoutUrl);
        },
        error: () => {
          this.toastr.error(this.translocoService.translate('renew-license.link-sent-error'));
          this.isSending.set(false);
        }
      });
  }
}
