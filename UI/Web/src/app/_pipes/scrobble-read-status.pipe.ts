import { Pipe, PipeTransform } from '@angular/core';
import {ScrobbleReadStatus} from "../_models/kavitaplus/scrobble-providers/scrobble-read-status.enum";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'scrobbleReadStatus',
})
export class ScrobbleReadStatusPipe implements PipeTransform {

  transform(value: ScrobbleReadStatus): string {
    switch (value) {
      case ScrobbleReadStatus.Ignore:
        return translate('scrobble-read-status-pipe.ignore');
      case ScrobbleReadStatus.WantToRead:
        return translate('scrobble-read-status-pipe.want-to-read');
      case ScrobbleReadStatus.Read:
        return translate('scrobble-read-status-pipe.read');
      case ScrobbleReadStatus.UnRead:
        return translate('scrobble-read-status-pipe.unread');
      case ScrobbleReadStatus.Dropped:
        return translate('scrobble-read-status-pipe.dropped');
      case ScrobbleReadStatus.OnHold:
        return translate('scrobble-read-status-pipe.on-hold');
    }
  }

}
