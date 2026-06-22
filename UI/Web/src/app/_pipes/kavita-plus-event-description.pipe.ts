import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusEventType} from '../_models/kavitaplus/kavita-plus-event-type.enum';
import {ScrobbleEventType} from '../_models/scrobbling/scrobble-event';
import {EntityTitleService} from '../_services/entity-title.service';
import {ScrobbleReadStatusPipe} from "./scrobble-read-status.pipe";
import {ScrobbleProviderNamePipe} from "./scrobble-provider-name.pipe";
import {UtcToLocalTimePipe} from "./utc-to-local-time.pipe";
import {AuditStatus} from "../_models/kavitaplus/audit-status.enum";

const PREFIX = 'kavita-plus-event-description-pipe';

@Pipe({
  name: 'kavitaPlusEventDescription',
  standalone: true,
})
export class KavitaPlusEventDescriptionPipe implements PipeTransform {

  private readonly readStatusPipe = new ScrobbleReadStatusPipe();
  private readonly providerNamePipe = new ScrobbleProviderNamePipe();
  private readonly utcToLocalTimePipe = new UtcToLocalTimePipe();
  private readonly translocoService = inject(TranslocoService);
  private readonly entityTitleService = inject(EntityTitleService);

  transform(entry: KavitaPlusAuditEntry): string {
    const sd = entry.scrobbleDetails;
    if (sd) {
      switch (sd.scrobbleEventType) {
        case ScrobbleEventType.ChapterRead: {
          // Note: there can be a discrepancy where creation event says Ch 2 and Sent event says Vol 0 Ch 2 due to
          // -100000 being overridden to 0 on send
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
        case ScrobbleEventType.ReadStatusUpdate:
          return this.translocoService.translate(`${PREFIX}.read-status-update`, {status: this.readStatusPipe.transform(sd.readStatus!)});
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

    if (entry.eventType === KavitaPlusEventType.PersonAliasAdded) {
      return this.translocoService.translate(`${PREFIX}.person-alias-added`, {personName: entry.metadataExtras?.personName, alias: entry.metadataExtras?.aliasAdded});
    }

    if (entry.eventType === KavitaPlusEventType.SeriesMatched) {
      return this.translocoService.translate(`${PREFIX}.series-matched-against`, {matchName: entry.matchDetails?.matchedName});
    }

    if (entry.eventType === KavitaPlusEventType.SystemTokenRefresh) {
      if (entry.status === AuditStatus.Success) {
        return this.translocoService.translate(`${PREFIX}.system-token-refresh-success`,
          {
            provider: this.providerNamePipe.transform(entry.systemDetails!.provider),
            validUntil: this.utcToLocalTimePipe.transform(entry.systemDetails!.validUntilUtc)
          });
      }

      return this.translocoService.translate(`${PREFIX}.system-token-refresh-failure`,
        {
          provider: this.providerNamePipe.transform(entry.systemDetails!.provider),
        });
    }

    if (entry.eventType === KavitaPlusEventType.SystemProviderInfoSync) {
      if (entry.status === AuditStatus.Success) {
        return this.translocoService.translate(`${PREFIX}.system-provider-info-sync-success`,
          {
            provider: this.providerNamePipe.transform(entry.systemDetails!.provider),
            username: entry.systemDetails!.userInfo!.username
          });
      }

      return this.translocoService.translate(`${PREFIX}.system-provider-info-sync-failure`,
        {
          provider: this.providerNamePipe.transform(entry.systemDetails!.provider),
        });
    }

    return '';
  }
}
