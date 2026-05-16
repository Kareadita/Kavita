import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {DatePipe} from '@angular/common';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from '../../_models/kavitaplus/kavita-plus-event-type.enum';
import {AuditStatus} from '../../_models/kavitaplus/audit-status.enum';
import {KavitaPlusEventTypePipe} from '../../_pipes/kavita-plus-event-type.pipe';
import {ScrobbleProviderNamePipe} from '../../_pipes/scrobble-provider-name.pipe';
import {TimeAgoPipe} from '../../_pipes/time-ago.pipe';
import {UtcToLocalTimePipe} from '../../_pipes/utc-to-local-time.pipe';
import {ImageService} from '../../_services/image.service';
import {EntityTitleService} from '../../_services/entity-title.service';
import {ImageComponent} from '../../shared/image/image.component';
import {
  ScrobbleProviderImageComponent
} from '../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';

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
    ScrobbleProviderNamePipe,
    TimeAgoPipe,
    UtcToLocalTimePipe,
    DatePipe,
    ImageComponent,
    ScrobbleProviderImageComponent,
  ],
})
export class KavitaplusTimelineComponent {
  protected readonly imageService = inject(ImageService);
  private readonly entityTitleService = inject(EntityTitleService);

  entries = input.required<KavitaPlusAuditEntry[]>();
  isLoading = input<boolean>(false);
  showRetry = input<boolean>(true);

  groupedEntries = computed(() => groupByDay(this.entries()));

  categoryColorClass(category: KavitaPlusAuditCategory): string {
    switch (category) {
      case KavitaPlusAuditCategory.Match:    return 'match';
      case KavitaPlusAuditCategory.Scrobble: return 'scrobble';
      case KavitaPlusAuditCategory.Sync:     return 'sync';
      default:                               return 'metadata';
    }
  }

  descriptionColorClass(entry: KavitaPlusAuditEntry): string {
    if (entry.status === AuditStatus.Failure) return 'failure';
    return this.categoryColorClass(entry.category);
  }

  descriptionIcon(entry: KavitaPlusAuditEntry): string {
    if (entry.status === AuditStatus.Failure) return 'fa-triangle-exclamation';
    if (entry.category === KavitaPlusAuditCategory.Scrobble) return 'fa-paper-plane';
    if (entry.category === KavitaPlusAuditCategory.Match)    return 'fa-link';
    if (entry.category === KavitaPlusAuditCategory.Sync)     return 'fa-arrows-rotate';
    if (entry.eventType === KavitaPlusEventType.CoverUpdated ||
        entry.eventType === KavitaPlusEventType.ChapterCoverUpdated ||
        entry.eventType === KavitaPlusEventType.PersonCoverUpdated) return 'fa-image';
    return 'fa-pen-to-square';
  }

  descriptionDetail(entry: KavitaPlusAuditEntry): string {
    if (!entry.scrobbleDetails) return '';
    const label = this.entityTitleService.scrobbleDetailLabel(entry.scrobbleDetails);
    return label ? ` - ${label}` : '';
  }

  isRetryable(entry: KavitaPlusAuditEntry): boolean {
    return entry.status === AuditStatus.Failure && entry.category === KavitaPlusAuditCategory.Scrobble;
  }

  protected readonly AuditStatus = AuditStatus;
  protected readonly KavitaPlusEventType = KavitaPlusEventType;
}
