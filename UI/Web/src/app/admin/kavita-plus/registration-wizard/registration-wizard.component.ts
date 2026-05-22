import {ChangeDetectionStrategy, Component, signal} from '@angular/core';
import {KavitaPlusUpsellComponent} from "../kavita-plus-upsell/kavita-plus-upsell.component";
import {
  KavitaPlusConnectProvidersComponent
} from "../kavita-plus-connect-providers/kavita-plus-connect-providers.component";

export enum KavitaPlusRegistrationStep {
  Upsell = 0,
  ConnectProviders = 1
}

@Component({
  selector: 'app-registration-wizard',
  imports: [
    KavitaPlusUpsellComponent,
    KavitaPlusConnectProvidersComponent
  ],
  templateUrl: './registration-wizard.component.html',
  styleUrl: './registration-wizard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegistrationWizardComponent {

  activeStep = signal<KavitaPlusRegistrationStep>(KavitaPlusRegistrationStep.Upsell);

  protected readonly KavitaPlusRegistrationStep = KavitaPlusRegistrationStep;
}
