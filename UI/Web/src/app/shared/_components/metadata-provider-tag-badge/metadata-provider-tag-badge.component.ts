import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {MetadataProvider} from "../../../_models/kavitaplus/metadata-provider.enum";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ScrobbleProviderTagBadgeComponent} from "../scrobble-provider-tag-badge/scrobble-provider-tag-badge.component";

@Component({
  selector: 'app-metadata-provider-tag-badge',
  imports: [
    ScrobbleProviderTagBadgeComponent
  ],
  templateUrl: './metadata-provider-tag-badge.component.html',
  styleUrl: './metadata-provider-tag-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetadataProviderTagBadgeComponent {

  metadataProvider = input.required<MetadataProvider>();

  protected mappedScrobbleProvider = computed(() => {
    switch (this.metadataProvider()) {
      case MetadataProvider.Hardcover:
        return ScrobbleProvider.Hardcover;
      case MetadataProvider.Mangabaka:
        return ScrobbleProvider.MangaBaka;
      case MetadataProvider.ComicBookRoundup:
        return ScrobbleProvider.Cbr;
    }
  });

}
