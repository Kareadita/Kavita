import {inject, Pipe, PipeTransform} from '@angular/core';
import {ScrobbleEventType} from "../_models/scrobbling/scrobble-event";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'scrobbleEventType',
  standalone: true
})
export class ScrobbleEventTypePipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(value: ScrobbleEventType): string {
    switch (value) {
      case ScrobbleEventType.ChapterRead:
        return this.translocoService.translate('scrobble-event-type-pipe.chapter-read');
      case ScrobbleEventType.ScoreUpdated:
        return this.translocoService.translate('scrobble-event-type-pipe.score-updated');
      case ScrobbleEventType.AddWantToRead:
        return this.translocoService.translate('scrobble-event-type-pipe.want-to-read-add');
      case ScrobbleEventType.RemoveWantToRead:
        return this.translocoService.translate('scrobble-event-type-pipe.want-to-read-remove');
      case ScrobbleEventType.Review:
        return this.translocoService.translate('scrobble-event-type-pipe.review');
    }
  }

}
