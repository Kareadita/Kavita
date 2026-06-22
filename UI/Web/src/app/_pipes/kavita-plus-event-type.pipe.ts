import {inject, Pipe, PipeTransform} from '@angular/core';
import {KavitaPlusEventType} from '../_models/kavitaplus/kavita-plus-event-type.enum';
import {TranslocoService} from '@jsverse/transloco';

@Pipe({
  name: 'kavitaPlusEventType',
  standalone: true
})
export class KavitaPlusEventTypePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: KavitaPlusEventType): string {
    switch (value) {
      case KavitaPlusEventType.SeriesMatched:
        return this.translocoService.translate('kavita-plus-event-type-pipe.series-matched');
      case KavitaPlusEventType.SeriesMatchFailed:
        return this.translocoService.translate('kavita-plus-event-type-pipe.series-match-failed');
      case KavitaPlusEventType.SeriesBlacklisted:
        return this.translocoService.translate('kavita-plus-event-type-pipe.series-blacklisted');
      case KavitaPlusEventType.SeriesMatchFixed:
        return this.translocoService.translate('kavita-plus-event-type-pipe.series-match-fixed');
      case KavitaPlusEventType.SeriesDontMatchSet:
        return this.translocoService.translate('kavita-plus-event-type-pipe.series-dont-match-set');
      case KavitaPlusEventType.MetadataFetched:
        return this.translocoService.translate('kavita-plus-event-type-pipe.metadata-fetched');
      case KavitaPlusEventType.MetadataUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.metadata-updated');
      case KavitaPlusEventType.CoverUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.cover-updated');
      case KavitaPlusEventType.ChapterMetadataUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.chapter-metadata-updated');
      case KavitaPlusEventType.ChapterCoverUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.chapter-cover-updated');
      case KavitaPlusEventType.PersonCoverUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.person-cover-updated');
      case KavitaPlusEventType.PersonAliasAdded:
        return this.translocoService.translate('kavita-plus-event-type-pipe.person-alias-added');
      case KavitaPlusEventType.CollectionSynced:
        return this.translocoService.translate('kavita-plus-event-type-pipe.collection-synced');
      case KavitaPlusEventType.CollectionItemAdded:
        return this.translocoService.translate('kavita-plus-event-type-pipe.collection-item-added');
      case KavitaPlusEventType.ScrobbleEventCreated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-created');
      case KavitaPlusEventType.ScrobbleEventUpdated:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-updated');
      case KavitaPlusEventType.ScrobbleEventSent:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-sent');
      case KavitaPlusEventType.ScrobbleEventFailed:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-failed');
      case KavitaPlusEventType.ScrobbleRateLimitHit:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-rate-limit');
      case KavitaPlusEventType.ScrobbleEventSkipped:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-skipped');
      case KavitaPlusEventType.ScrobbleHoldAdded:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-hold-added');
      case KavitaPlusEventType.ScrobbleHoldRemoved:
        return this.translocoService.translate('kavita-plus-event-type-pipe.scrobble-hold-removed');
      case KavitaPlusEventType.SyncStarted:
        return this.translocoService.translate('kavita-plus-event-type-pipe.sync-started');
      case KavitaPlusEventType.SyncCompleted:
        return this.translocoService.translate('kavita-plus-event-type-pipe.sync-completed');
      case KavitaPlusEventType.SyncFailed:
        return this.translocoService.translate('kavita-plus-event-type-pipe.sync-failed');
      case KavitaPlusEventType.SystemProviderInfoSync:
        return this.translocoService.translate('kavita-plus-event-type-pipe.system-provider-info-sync');
      case KavitaPlusEventType.SystemTokenRefresh:
        return this.translocoService.translate('kavita-plus-event-type-pipe.system-token-refresh');
    }
  }
}
