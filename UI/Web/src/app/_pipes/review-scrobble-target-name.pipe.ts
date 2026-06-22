import { Pipe, PipeTransform } from '@angular/core';
import {ReviewScrobbleTarget} from "../_models/kavitaplus/scrobble-providers/review-scrobble-target.enum";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'reviewScrobbleTargetName',
})
export class ReviewScrobbleTargetNamePipe implements PipeTransform {

  transform(value: ReviewScrobbleTarget): string {
    switch (value) {
      case ReviewScrobbleTarget.Private:
        return translate('review-scrobble-target-name-pipe.private');
      case ReviewScrobbleTarget.Friends:
        return translate('review-scrobble-target-name-pipe.friends');
      case ReviewScrobbleTarget.Public:
        return translate('review-scrobble-target-name-pipe.public');
    }
  }

}
