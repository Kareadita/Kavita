import { Pipe, PipeTransform } from '@angular/core';
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'scrobbleProviderDescription',
})
export class ScrobbleProviderDescriptionPipe implements PipeTransform {

  transform(value: ScrobbleProvider): string {
    switch (value) {
      case ScrobbleProvider.Kavita:
        return translate('scrobble-provider-description-pipe.kavita');
      case ScrobbleProvider.AniList:
        return translate('scrobble-provider-description-pipe.ani-list');
      case ScrobbleProvider.Mal:
        return translate('scrobble-provider-description-pipe.my-anime-list');
      case ScrobbleProvider.Cbr:
        return translate('scrobble-provider-description-pipe.comic-book-roundup');
      case ScrobbleProvider.Hardcover:
        return translate('scrobble-provider-description-pipe.hardcover');
      case ScrobbleProvider.MangaBaka:
        return translate('scrobble-provider-description-pipe.manga-baka');
    }
  }

}
