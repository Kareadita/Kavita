import {Pipe, PipeTransform} from '@angular/core';
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'scrobbleProviderDescription',
})
export class ScrobbleProviderDescriptionPipe implements PipeTransform {

  transform(value: ScrobbleProvider): string {
    switch (value) {
      case ScrobbleProvider.Cbr:
      case ScrobbleProvider.Kavita:
        return ''; // These don't have any description and leaving a blank key in en.json breaks weblate
      case ScrobbleProvider.AniList:
        return translate('scrobble-provider-description-pipe.anilist');
      case ScrobbleProvider.Mal:
        return translate('scrobble-provider-description-pipe.my-anime-list');
      case ScrobbleProvider.Hardcover:
        return translate('scrobble-provider-description-pipe.hardcover');
      case ScrobbleProvider.MangaBaka:
        return translate('scrobble-provider-description-pipe.mangabaka');
    }
  }

}
