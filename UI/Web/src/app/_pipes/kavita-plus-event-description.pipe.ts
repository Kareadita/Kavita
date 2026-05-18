import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusEventType} from '../_models/kavitaplus/kavita-plus-event-type.enum';
import {ScrobbleEventType} from '../_models/scrobbling/scrobble-event';
import {EntityTitleService} from '../_services/entity-title.service';

const PREFIX = 'kavita-plus-event-description-pipe';

@Pipe({
  name: 'kavitaPlusEventDescription',
  standalone: true,
})
export class KavitaPlusEventDescriptionPipe implements PipeTransform {
  private readonly t = inject(TranslocoService);
  private readonly entityTitleService = inject(EntityTitleService);

  transform(entry: KavitaPlusAuditEntry): string {
    const sd = entry.scrobbleDetails;
    if (sd) {
      switch (sd.scrobbleEventType) {
        case ScrobbleEventType.ChapterRead: {
          const chapter = this.entityTitleService.scrobbleDetailLabel(sd);
          return chapter ? this.t.translate(`${PREFIX}.read-progress-sent`, {chapter}) : '';
        }
        case ScrobbleEventType.ScoreUpdated:
          return this.t.translate(`${PREFIX}.rating-updated`);
        case ScrobbleEventType.AddWantToRead:
          return this.t.translate(`${PREFIX}.add-want-to-read`);
        case ScrobbleEventType.RemoveWantToRead:
          return this.t.translate(`${PREFIX}.remove-want-to-read`);
        case ScrobbleEventType.Review:
          return this.t.translate(`${PREFIX}.review-submitted`);
        default:
          return '';
      }
    }

    if (
      (entry.eventType === KavitaPlusEventType.MetadataUpdated ||
        entry.eventType === KavitaPlusEventType.ChapterMetadataUpdated) &&
      entry.diff?.length
    ) {
      return this.t.translate(`${PREFIX}.fields-updated`, {count: entry.diff.length});
    }

    return '';
  }
}
