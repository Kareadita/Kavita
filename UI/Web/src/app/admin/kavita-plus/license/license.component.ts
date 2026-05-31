import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {ReactiveFormsModule} from "@angular/forms";
import {AccountService} from "../../../_services/account.service";
import {LicenseService} from "../../../_services/license.service";
import {WikiLink} from "../../../_models/wiki";
import {LicenseDashboardComponent} from "../license-dashboard/license-dashboard.component";
import {KavitaPlusUpsellComponent} from "../kavita-plus-upsell/kavita-plus-upsell.component";
import {
  KavitaPlusConnectProvidersComponent
} from "../kavita-plus-connect-providers/kavita-plus-connect-providers.component";

export enum KavitaPlusRegistrationStep {
  Upsell = 0,
  ConnectProviders = 1
}

@Component({
    selector: 'app-license',
    templateUrl: './license.component.html',
    styleUrls: ['./license.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LicenseDashboardComponent, KavitaPlusUpsellComponent, KavitaPlusConnectProvidersComponent]
})
export class LicenseComponent implements OnInit {

  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);

  protected readonly WikiLink = WikiLink;

  activeStep = signal<KavitaPlusRegistrationStep>(KavitaPlusRegistrationStep.Upsell);

  isChecking = signal<boolean>(true);

  protected readonly KavitaPlusRegistrationStep = KavitaPlusRegistrationStep;


  ngOnInit(): void {
    this.loadLicenseInfo();
  }

  loadLicenseInfo(forceCheck = false) {
    this.isChecking.set(true);
    this.licenseService.getLicenseInfo(forceCheck).subscribe({
      next: () => this.isChecking.set(false),
      error: () => this.isChecking.set(false),
    });
  }
}
