import {ChangeDetectionStrategy, Component, computed, inject, input, output, TemplateRef} from '@angular/core';
import {DatePipe, NgTemplateOutlet} from '@angular/common';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {AuditStatus} from '../../_models/kavitaplus/audit-status.enum';
import {KavitaPlusEventTypePipe} from '../../_pipes/kavita-plus-event-type.pipe';
import {KavitaPlusEventDescriptionPipe} from '../../_pipes/kavita-plus-event-description.pipe';
import {ScrobbleProviderNamePipe} from '../../_pipes/scrobble-provider-name.pipe';
import {TimeAgoPipe} from '../../_pipes/time-ago.pipe';
import {UtcToLocalTimePipe} from '../../_pipes/utc-to-local-time.pipe';
import {ImageService} from '../../_services/image.service';
import {ImageComponent} from '../../shared/image/image.component';
import {
  ScrobbleProviderImageComponent
} from '../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {AuditLogErrorPipe} from "../../_pipes/audit-log-error.pipe";
import {
  KavitaPlusAuditEventTypeIconComponent
} from "../../shared/_components/kavitaplus-event-type-icon/kavita-plus-audit-event-type-icon.component";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";

interface DayGroup {
  key: string;
  label: 'today' | 'yesterday' | 'date';
  dateStr: string;
  count: number;
  events: KavitaPlusAuditEntry[];
}

function groupByDay(entries: KavitaPlusAuditEntry[]): DayGroup[] {
  const now = new Date();
  const todayKey = now.toISOString().slice(0, 10);
  const yesterdayKey = new Date(now.getTime() - 86_400_000).toISOString().slice(0, 10);
  const map = new Map<string, KavitaPlusAuditEntry[]>();

  for (const entry of entries) {
    const key = entry.createdUtc.slice(0, 10);
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(entry);
  }

  return Array.from(map.entries())
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([key, evts]) => ({
      key,
      label: key === todayKey ? 'today' : key === yesterdayKey ? 'yesterday' : 'date',
      dateStr: key,
      count: evts.length,
      events: evts,
    }));
}

@Component({
  selector: 'app-kavitaplus-timeline',
  templateUrl: './kavitaplus-timeline.component.html',
  styleUrls: ['./kavitaplus-timeline.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoDirective,
    KavitaPlusEventTypePipe,
    KavitaPlusEventDescriptionPipe,
    ScrobbleProviderNamePipe,
    TimeAgoPipe,
    UtcToLocalTimePipe,
    DatePipe,
    ImageComponent,
    ScrobbleProviderImageComponent,
    AuditLogErrorPipe,
    KavitaPlusAuditEventTypeIconComponent,
    NgTemplateOutlet,
    EmptyStateComponent,
  ],
})
export class KavitaplusTimelineComponent {
  protected readonly imageService = inject(ImageService);
  private readonly router = inject(Router);

  entries = input.required<KavitaPlusAuditEntry[]>();
  isLoading = input<boolean>(false);
  showRetry = input<boolean>(true);
  hasMore = input<boolean>(false);
  isLoadingMore = input<boolean>(false);
  entryTemplate = input<TemplateRef<{$implicit: KavitaPlusAuditEntry}>>();

  retry = output<KavitaPlusAuditEntry>();
  loadMore = output<void>();

  groupedEntries = computed(() => groupByDay(this.entries()));

  categoryColor(category: KavitaPlusAuditCategory): string {
    switch (category) {
      case KavitaPlusAuditCategory.Match:    return 'var(--audit-log-match-color)';
      case KavitaPlusAuditCategory.Scrobble: return 'var(--audit-log-scrobble-color)';
      case KavitaPlusAuditCategory.Sync:     return 'var(--audit-log-sync-color)';
      default:                               return 'var(--audit-log-metadata-color)';
    }
  }

  categoryBg(category: KavitaPlusAuditCategory): string {
    return `color-mix(in srgb, ${this.categoryColor(category)} 12%, transparent)`;
  }

  descriptionColor(entry: KavitaPlusAuditEntry): string {
    return entry.status === AuditStatus.Failure
      ? 'var(--toast-warning-bg-color)'
      : '';
  }

  navigateToSeries(entry: KavitaPlusAuditEntry): void {
    if (entry.seriesId == null || entry.libraryId == null) return;
    this.router.navigate(['library', entry.libraryId, 'series', entry.seriesId]);
  }

  retryScrobbleEvent(entry: KavitaPlusAuditEntry) {
    this.retry.emit(entry);
  }


}
