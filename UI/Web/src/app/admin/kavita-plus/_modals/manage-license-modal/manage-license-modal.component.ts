import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from '@jsverse/transloco';
import {environment} from '../../../../../environments/environment';
import {LicenseService} from '../../../../_services/license.service';
import {KavitaPlusSubscriptionState} from '../../../../_models/kavitaplus/license-info';
import {RenewLicenseComponent} from '../../renew-license/renew-license.component';
import {CancelLicenseComponent} from "../../cancel-license/cancel-license.component";
import {ChangeLicenseEmailComponent} from "../../change-license-email/change-license-email.component";

export enum ManageLicenseStep {
  Hub = 'hub',
  Renew = 'renew',
  Cancel = 'cancel',
  ChangeEmail = 'change-email',
}

@Component({
  selector: 'app-manage-license-modal',
  imports: [TranslocoDirective, RenewLicenseComponent, CancelLicenseComponent, ChangeLicenseEmailComponent],
  templateUrl: './manage-license-modal.component.html',
  styleUrl: './manage-license-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageLicenseModalComponent {

  protected readonly modal = inject(NgbActiveModal);
  private readonly licenseService = inject(LicenseService);

  protected readonly step = signal<ManageLicenseStep>(ManageLicenseStep.Hub);

  protected readonly licenseInfo = this.licenseService.licenseInfo;

  protected readonly isActiveSubscription = computed((): boolean => {
    const state = this.licenseInfo()?.state;
    return state === KavitaPlusSubscriptionState.Active
      || state === KavitaPlusSubscriptionState.Cancelling
      || state === KavitaPlusSubscriptionState.Paused;
  });

  protected readonly isCancelling = computed((): boolean =>
    this.licenseInfo()?.state === KavitaPlusSubscriptionState.Cancelling);

  protected readonly manageLink = computed((): string => {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email) return environment.manageLink;
    return environment.manageLink + '?prefilled_email=' + encodeURIComponent(email);
  });

  goTo(step: ManageLicenseStep) {
    this.step.set(step);
  }

  back() {
    this.step.set(ManageLicenseStep.Hub);
  }

  close() {
    this.modal.dismiss();
  }

  protected readonly ManageLicenseStep = ManageLicenseStep;
  protected readonly KavitaPlusSubscriptionState = KavitaPlusSubscriptionState;
}
