import {ChangeDetectionStrategy, Component, computed, inject, input, signal} from '@angular/core';
import {NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {NgClass} from '@angular/common';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from '../../../_models/kavitaplus/kavita-plus-event-type.enum';
import {AuditStatus} from '../../../_models/kavitaplus/audit-status.enum';
import {ImageService} from '../../../_services/image.service';
import {ImageComponent} from '../../../shared/image/image.component';
import {ProfileIconComponent} from '../../../_single-module/profile-icon/profile-icon.component';
import {
  ScrobbleProviderImageComponent
} from '../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {ScrobbleProvider} from '../../../_services/scrobbling.service';
import {ScrobbleProviderNamePipe} from '../../../_pipes/scrobble-provider-name.pipe';
import {
  ScrobbleProviderTagBadgeComponent
} from '../../../shared/_components/scrobble-provider-tag-badge/scrobble-provider-tag-badge.component';
import {KavitaPlusEventTypePipe} from '../../../_pipes/kavita-plus-event-type.pipe';
import {KavitaPlusEventDescriptionPipe} from '../../../_pipes/kavita-plus-event-description.pipe';
import {AuditLogErrorPipe} from '../../../_pipes/audit-log-error.pipe';
import {TimeAgoPipe} from '../../../_pipes/time-ago.pipe';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {AuditStatusTitlePipe} from "../../../_pipes/audit-status-title.pipe";
import {KavitaplusDiffComponent} from "../kavitaplus-diff/kavitaplus-diff.component";
import {AuditSubjectType} from "../../../_models/kavitaplus/audit-subject-type.enum";
import {MetadataFetchTriggerTitlePipe} from "../../../_pipes/metadata-fetch-trigger-title.pipe";
import {TruncatePipe} from "../../../_pipes/truncate.pipe";

@Component({
  selector: 'app-kavitaplus-audit-accordion-item',
  imports: [
    NgbCollapse,
    NgClass,
    TranslocoDirective,
    ImageComponent,
    ProfileIconComponent,
    ScrobbleProviderImageComponent,
    ScrobbleProviderNamePipe,
    ScrobbleProviderTagBadgeComponent,
    KavitaPlusEventTypePipe,
    KavitaPlusEventDescriptionPipe,
    AuditLogErrorPipe,
    TimeAgoPipe,
    UtcToLocalTimePipe,
    AuditStatusTitlePipe,
    KavitaplusDiffComponent,
    TruncatePipe,
    MetadataFetchTriggerTitlePipe,
  ],
  templateUrl: './kavitaplus-audit-accordion-item.component.html',
  styleUrl: './kavitaplus-audit-accordion-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaplusAuditAccordionItemComponent {
  protected readonly imageService = inject(ImageService);
  private readonly router = inject(Router);

  entry = input.required<KavitaPlusAuditEntry>();

  collapsed = signal(true);

  entityLabel = computed(() => {
    const e = this.entry();
    if (e.seriesName) return e.seriesName;
    if (e.metadataExtras?.personName) return e.metadataExtras.personName;
    if (e.syncDetails?.collectionName) return e.syncDetails.collectionName;
    return null;
  });

  matchProviderBadges = computed(() => {
    const ids = this.entry().matchDetails?.after;
    if (!ids) return [];
    const badges: {provider: ScrobbleProvider; id: number}[] = [];
    if (ids.aniListId) badges.push({provider: ScrobbleProvider.AniList, id: ids.aniListId});
    if (ids.malId) badges.push({provider: ScrobbleProvider.Mal, id: ids.malId});
    if (ids.mangaBakaId) badges.push({provider: ScrobbleProvider.MangaBaka, id: ids.mangaBakaId});
    if (ids.cbrId) badges.push({provider: ScrobbleProvider.Cbr, id: ids.cbrId});
    if (ids.hardcoverId) badges.push({provider: ScrobbleProvider.Hardcover, id: ids.hardcoverId});
    return badges;
  });

  fetchTrigger = computed(() => {
    const e = this.entry();
    if (e.eventType !== KavitaPlusEventType.MetadataFetched) return null;
    // MetadataFetchTrigger.Unknown (0) is falsy, so this also filters out untracked/legacy entries
    return e.metadataExtras?.fetchTrigger || null;
  });

  statusBadgeClass = computed(() => {
    switch (this.entry().status) {
      case AuditStatus.Success:
        return 'bg-success';
      case AuditStatus.Failure:
        return 'bg-danger';
      default:
        return 'bg-secondary';
    }
  });

  supportsDiff(entry: KavitaPlusAuditEntry) {
    return [KavitaPlusEventType.MetadataUpdated, KavitaPlusEventType.ChapterMetadataUpdated].includes(entry.eventType);
  }

  navigateToSeries() {
    const e = this.entry();
    if (e.seriesId == null || e.libraryId == null) return;
    this.router.navigate(['library', e.libraryId, 'series', e.seriesId]);
  }

  protected readonly KavitaPlusAuditCategory = KavitaPlusAuditCategory;
  protected readonly AuditSubjectType = AuditSubjectType;
}
