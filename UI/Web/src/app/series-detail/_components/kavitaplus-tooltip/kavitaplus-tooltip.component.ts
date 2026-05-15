import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {Router} from '@angular/router';
import {NgbActiveOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective, TranslocoService} from '@jsverse/transloco';
import {AccountService} from '../../../_services/account.service';
import {KavitaPlusAuditService} from '../../../_services/kavitaplus-audit.service';
import {KavitaPlusAuditSeriesInfo} from '../../../_models/kavitaplus/kavita-plus-audit-series-info';
import {KavitaPlusAuditCategory} from '../../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from '../../../_models/kavitaplus/kavita-plus-event-type.enum';
import {ScrobbleEventType} from '../../../_models/scrobbling/scrobble-event';
import {TimeAgoPipe} from '../../../_pipes/time-ago.pipe';
import {KavitaPlusEventTypePipe} from '../../../_pipes/kavita-plus-event-type.pipe';
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";

@Component({
  selector: 'app-kavitaplus-tooltip',
  templateUrl: './kavitaplus-tooltip.component.html',
  styleUrls: ['./kavitaplus-tooltip.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, TimeAgoPipe, KavitaPlusEventTypePipe, UtcToLocalTimePipe],
})
export class KavitaplusTooltipComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
  private readonly translocoService = inject(TranslocoService);
  private readonly router = inject(Router);
  protected readonly activeOffcanvas = inject(NgbActiveOffcanvas, { optional: true });
  protected readonly isAdmin = inject(AccountService).hasAdminRole;

  seriesId = input.required<number>();

  seriesInfo = signal<KavitaPlusAuditSeriesInfo | null>(null);
  categoryFilter = signal<KavitaPlusAuditCategory | null>(null);
  isLoading = signal(true);

  filteredEvents = computed(() => {
    const info = this.seriesInfo();
    if (!info) return [];
    const f = this.categoryFilter();
    return f === null ? info.recentEvents : info.recentEvents.filter(e => e.category === f);
  });

  displayedEvents = computed(() => this.filteredEvents().slice(0, 5));

  totalCount    = computed(() => this.seriesInfo()?.recentEvents.length ?? 0);
  metadataCount = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Metadata).length ?? 0);
  scrobbleCount = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Scrobble).length ?? 0);
  matchCount    = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Match).length ?? 0);

  ngOnInit() {
    this.auditService.getSeriesInfo(this.seriesId()).subscribe({
      next: info => { this.seriesInfo.set(info); this.isLoading.set(false); },
      error: ()   => this.isLoading.set(false),
    });
  }

  setFilter(cat: KavitaPlusAuditCategory | null) {
    this.categoryFilter.set(cat);
  }

  close() {
    this.activeOffcanvas?.close();
  }

  navigateAndClose(commands: unknown[]) {
    this.activeOffcanvas?.close();
    this.router.navigate(commands);
  }

  chapterLabel(chapterNumber: number | null, volumeNumber: number | null): string {
    if (chapterNumber === null && volumeNumber === null) return '';
    const chStr = chapterNumber !== null
      ? this.translocoService.translate('common.chapter-num-shorthand', { num: chapterNumber })
      : null;
    const volStr = volumeNumber !== null
      ? this.translocoService.translate('common.volume-num-shorthand', { num: volumeNumber })
      : null;
    if (volStr && chStr) return `${volStr} ${chStr}`;
    return chStr ?? volStr ?? '';
  }

  categoryIcon(category: KavitaPlusAuditCategory, eventType: KavitaPlusEventType): string {
    if (category === KavitaPlusAuditCategory.Scrobble) return 'fa-bookmark';
    if (category === KavitaPlusAuditCategory.Match)    return 'fa-link';
    if (category === KavitaPlusAuditCategory.Sync)     return 'fa-arrows-rotate';
    if (eventType === KavitaPlusEventType.CoverUpdated ||
        eventType === KavitaPlusEventType.ChapterCoverUpdated ||
        eventType === KavitaPlusEventType.PersonCoverUpdated) return 'fa-image';
    return 'fa-pen-to-square';
  }

  categoryColorClass(category: KavitaPlusAuditCategory): string {
    switch (category) {
      case KavitaPlusAuditCategory.Match:    return 'match';
      case KavitaPlusAuditCategory.Scrobble: return 'scrobble';
      case KavitaPlusAuditCategory.Sync:     return 'sync';
      default:                               return 'metadata';
    }
  }

  protected readonly AuditCategory = KavitaPlusAuditCategory;
  protected readonly EventType = KavitaPlusEventType;
  protected readonly ScrobbleEventType = ScrobbleEventType;
}
