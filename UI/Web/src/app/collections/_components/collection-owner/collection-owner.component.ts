import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {UserCollection} from "../../../_models/collection-tag";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {ImageComponent} from "../../../shared/image/image.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";

@Component({
  selector: 'app-collection-owner',
  imports: [
    ProviderImagePipe,
    TranslocoDirective,
    ImageComponent,
    NgbTooltip,
    ScrobbleProviderNamePipe
  ],
  templateUrl: './collection-owner.component.html',
  styleUrl: './collection-owner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CollectionOwnerComponent {

  protected readonly accountService = inject(AccountService);
  protected readonly ScrobbleProvider = ScrobbleProvider;

  collection = input.required<UserCollection>();
}
