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
    } else if (entry.eventType === KavitaPlusEventType.CollectionSynced && entry.syncDetails) {
      return this.translocoService.translate(`${PREFIX}.collection-synced`, {
        collectionName: entry.syncDetails.collectionName,
        itemCount: entry.syncDetails.itemCount ?? 0,
        missingCount: entry.syncDetails.missingCount ?? 0,
      });
    } else if (entry.eventType === KavitaPlusEventType.CollectionItemAdded && entry.syncDetails?.collectionName) {
      return this.translocoService.translate(`${PREFIX}.collection-item-added`, {collectionName: entry.syncDetails.collectionName});
    } else if (entry.eventType === KavitaPlusEventType.PersonCoverUpdated && entry.metadataExtras?.personName) {
      return this.translocoService.translate(`${PREFIX}.person-cover-updated`, {personName: entry.metadataExtras.personName});
    } else if (entry.eventType === KavitaPlusEventType.PersonAliasAdded && entry.metadataExtras) {
      return this.translocoService.translate(`${PREFIX}.person-alias-added`, {
        aliasAdded: entry.metadataExtras.aliasAdded,
        personName: entry.metadataExtras.personName,
      });
    } else if (entry.eventType === KavitaPlusEventType.SyncStarted && entry.syncDetails?.collectionName) {
      return this.translocoService.translate(`${PREFIX}.sync-started-collection`, {
        collectionName: entry.syncDetails.collectionName,
        itemCount: entry.syncDetails.itemCount ?? 0,
      });
    } else if (entry.eventType === KavitaPlusEventType.SyncFailed && entry.syncDetails?.collectionName) {
      return this.translocoService.translate(`${PREFIX}.sync-failed-collection`, {collectionName: entry.syncDetails.collectionName});
    } else if (entry.eventType === KavitaPlusEventType.SyncCompleted && entry.syncDetails?.seriesMatched != null) {
      return this.translocoService.translate(`${PREFIX}.sync-completed-want-to-read`, {
        seriesMatched: entry.syncDetails.seriesMatched,
        userName: entry.syncDetails.userName,
      });
    }

    return '';
  }
}
