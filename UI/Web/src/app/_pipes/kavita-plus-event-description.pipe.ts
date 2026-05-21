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
  private readonly translocoService = inject(TranslocoService);
  private readonly entityTitleService = inject(EntityTitleService);

  transform(entry: KavitaPlusAuditEntry): string {
    const sd = entry.scrobbleDetails;
    if (sd) {
      switch (sd.scrobbleEventType) {
        case ScrobbleEventType.ChapterRead: {
          const chapter = this.entityTitleService.scrobbleDetailLabel(sd);
          return chapter ? this.translocoService.translate(`${PREFIX}.read-progress-sent`, {chapter}) : '';
        }
        case ScrobbleEventType.ScoreUpdated:
          return this.translocoService.translate(`${PREFIX}.rating-updated`, {rating: sd.rating});
        case ScrobbleEventType.AddWantToRead:
          return this.translocoService.translate(`${PREFIX}.add-want-to-read`);
        case ScrobbleEventType.RemoveWantToRead:
          return this.translocoService.translate(`${PREFIX}.remove-want-to-read`);
        case ScrobbleEventType.Review:
          return this.translocoService.translate(`${PREFIX}.review-submitted`);
        default:
          return '';
      }
    }

    if (
      (entry.eventType === KavitaPlusEventType.MetadataUpdated ||
        entry.eventType === KavitaPlusEventType.ChapterMetadataUpdated) &&
      entry.diff?.length
    ) {
      return this.translocoService.translate(`${PREFIX}.fields-updated`, {count: entry.diff.length});
    }

    if (entry.eventType === KavitaPlusEventType.ChapterCoverUpdated) {
      return this.translocoService.translate(`${PREFIX}.chapter-cover-updated`, {chapter: entry.metadataExtras!.issueNumber});
    } else if (entry.eventType === KavitaPlusEventType.CoverUpdated) {
      return this.translocoService.translate(`${PREFIX}.series-cover-updated`);
    } else if (entry.eventType === KavitaPlusEventType.SeriesMatchFixed) {
      return this.translocoService.translate(`${PREFIX}.series-match-fixed`, {matchName: entry.matchDetails?.matchedName});
    }

    return '';
  }
}
