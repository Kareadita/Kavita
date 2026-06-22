import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ScrobbleProviderImageComponent} from "../scrobble-provider-image/scrobble-provider-image.component";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {getProviderUrl} from "../../utils/provider-url.util";

const PROVIDER_BRAND_COLORS: Partial<Record<ScrobbleProvider, string>> = {
  [ScrobbleProvider.MangaBaka]: '#7c5cff',
  [ScrobbleProvider.AniList]:   '#02a9ff',
  [ScrobbleProvider.Mal]:       '#2e51a2',
  [ScrobbleProvider.Cbr]:       '#fc8452',
  [ScrobbleProvider.Hardcover]: '#c97aff',
};

@Component({
  selector: 'app-scrobble-provider-tag-badge',
  imports: [
    ScrobbleProviderImageComponent,
    ScrobbleProviderNamePipe,
    TranslocoDirective,
  ],
  templateUrl: './scrobble-provider-tag-badge.component.html',
  styleUrl: './scrobble-provider-tag-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScrobbleProviderTagBadgeComponent {
  provider = input.required<ScrobbleProvider>();
  id = input<number | null | undefined>(undefined);

  protected showId = computed(() => {
    const v = this.id();
    return v !== null && v !== undefined && v !== 0;
  });

  protected brandColor = computed(() => PROVIDER_BRAND_COLORS[this.provider()] ?? '#888');
  protected url = computed(() => this.showId() ? getProviderUrl(this.provider(), this.id()!) : null);
}
