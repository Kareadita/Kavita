import {ChangeDetectionStrategy, Component, computed, inject, input, output, signal} from '@angular/core';
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
import {AuditStatusTitlePipe} from "../../../_pipes/audit-status-title.pipe";
import {KavitaplusDiffComponent} from "../kavitaplus-diff/kavitaplus-diff.component";
import {AuditSubjectType} from "../../../_models/kavitaplus/audit-subject-type.enum";
import {MetadataFetchTriggerTitlePipe} from "../../../_pipes/metadata-fetch-trigger-title.pipe";
import {TruncatePipe} from "../../../_pipes/truncate.pipe";
import {UtcToLocalDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {TimeDifferencePipe} from "../../../_pipes/time-difference.pipe";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {SeriesService} from "../../../_services/series.service";
import {ActionService} from "../../../_services/action.service";
import {tap} from "rxjs";

@Component({
  selector: 'app-kavitaplus-audit-entry',
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
    AuditStatusTitlePipe,
    KavitaplusDiffComponent,
    TruncatePipe,
    MetadataFetchTriggerTitlePipe,
    UtcToLocalDatePipe,
    TimeDifferencePipe,
    SafeUrlPipe,
  ],
  templateUrl: './kavita-plus-audit-entry.component.html',
  styleUrl: './kavita-plus-audit-entry.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusAuditEntryComponent {

  protected readonly imageService = inject(ImageService);
  private readonly seriesService = inject(SeriesService);
  private readonly actionService = inject(ActionService);
  private readonly router = inject(Router);

  entry = input.required<KavitaPlusAuditEntry>();
  /** Show the status badge plus the match-provider and fetch-trigger badges (admin "rich" view). */
  showStatus = input<boolean>(false);
  /** Show the acting user's avatar and username. */
  showUser = input<boolean>(false);
  /** Show the retry button for retryable failures. */
  showRetry = input<boolean>(false);
  /** Show the collapsible metadata diff for events that support one. */
  showDiff = input<boolean>(false);

  retry = output<KavitaPlusAuditEntry>();

  collapsed = signal(true);

  entityLabel = computed(() => {
    const e = this.entry();
    if (e.seriesName) return e.seriesName;
    if (e.metadataExtras?.personName) return e.metadataExtras.personName;
    if (e.syncDetails?.collectionName) return e.syncDetails.collectionName;
    return null;
  });

  coverUrl = computed(() => {
    const e = this.entry();
    if (e.subjectId !== null && e.subjectType === AuditSubjectType.Chapter) {
      return this.imageService.getChapterCoverImage(e.subjectId);
    }
    if (e.subjectId !== null && e.subjectType === AuditSubjectType.Collection) {
      return this.imageService.getCollectionCoverImage(e.subjectId);
    }
    if (e.subjectId !== null && e.subjectType === AuditSubjectType.Person) {
      return this.imageService.getPersonImage(e.subjectId);
    }
    if (e.seriesId) {
      return this.imageService.getSeriesCoverImage(e.seriesId);
    }
    return null;
  });

  provider = computed(() => {
    if (!!this.entry().scrobbleDetails?.provider) {
      return this.entry().scrobbleDetails!.provider;
    }

    if (!!this.entry().systemDetails?.provider) {
      return this.entry().systemDetails!.provider;
    }

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

  descriptionColor = computed(() => {
    return this.entry().status === AuditStatus.Failure
      ? 'var(--toast-warning-bg-color)'
      : '';
  });

  supportsDiff = computed(() => {
    return [KavitaPlusEventType.MetadataUpdated, KavitaPlusEventType.ChapterMetadataUpdated].includes(this.entry().eventType);
  });

  navigateToSeries() {
    const e = this.entry();
    if (e.seriesId == null || e.libraryId == null) return;
    this.router.navigate(['library', e.libraryId, 'series', e.seriesId]);
  }

  retryEntry() {
    this.retry.emit(this.entry());
  }

  matchSeries() {
    const e = this.entry();
    if (e.seriesId == null) return;

    this.seriesService.getSeries(e.seriesId).pipe(
      tap(series => this.actionService.matchSeries(series))
    ).subscribe();
  }

  protected readonly KavitaPlusAuditCategory = KavitaPlusAuditCategory;
  protected readonly AuditSubjectType = AuditSubjectType;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
