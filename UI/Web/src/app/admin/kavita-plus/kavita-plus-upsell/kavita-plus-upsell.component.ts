import {ChangeDetectionStrategy, Component, output} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {ScrobbleProvider} from '../../../_services/scrobbling.service';
import {
  ScrobbleProviderImageComponent
} from '../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {ScrobbleProviderNamePipe} from '../../../_pipes/scrobble-provider-name.pipe';
import {environment} from "../../../../environments/environment";
import {RegisterLicenseKeyComponent} from "../register-license-key/register-license-key.component";
import {KavitaPlusRegistrationStep} from "../license/license.component";

@Component({
  selector: 'app-kavita-plus-upsell',
  imports: [
    TranslocoDirective,
    ScrobbleProviderImageComponent,
    ScrobbleProviderNamePipe,
    RegisterLicenseKeyComponent,
  ],
  templateUrl: './kavita-plus-upsell.component.html',
  styleUrl: './kavita-plus-upsell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusUpsellComponent {

  stepChanged = output<KavitaPlusRegistrationStep>();

  handleSaved(isSubActive: boolean) {
    // TODO: Prompt the user to inform them then move to Cancelled state
    if (!isSubActive) return;

    // TODO: Move to Connect Provider page
    this.stepChanged.emit(KavitaPlusRegistrationStep.ConnectProviders);
  }



  protected readonly WikiLink = WikiLink;
  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly providers = [
    ScrobbleProvider.AniList,
    ScrobbleProvider.Mal,
    ScrobbleProvider.Hardcover,
    ScrobbleProvider.MangaBaka,
  ];
  protected readonly environment = environment;
}
