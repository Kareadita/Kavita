import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {NgOptimizedImage} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-scrobble-provider-image',
  imports: [
    ScrobbleProviderNamePipe,
    ProviderImagePipe,
    NgOptimizedImage,
    NgbTooltip
  ],
  templateUrl: './scrobble-provider-image.component.html',
  styleUrl: './scrobble-provider-image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScrobbleProviderImageComponent {
  provider = input.required<ScrobbleProvider>();
  classes = input<string>('');
  size = input<number>(32);
  tooltip = input(false);
}
