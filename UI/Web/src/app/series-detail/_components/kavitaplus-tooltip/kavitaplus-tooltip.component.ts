import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {NavigationExtras, Router} from '@angular/router';
import {NgbActiveOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from '@jsverse/transloco';
import {AccountService} from '../../../_services/account.service';
import {KavitaPlusAuditService} from '../../../_services/kavitaplus-audit.service';
import {KavitaPlusAuditSeriesInfo} from '../../../_models/kavitaplus/kavita-plus-audit-series-info';
import {KavitaPlusAuditCategory} from '../../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {KavitaPlusEventTypePipe} from '../../../_pipes/kavita-plus-event-type.pipe';
import {KavitaPlusEventDescriptionPipe} from '../../../_pipes/kavita-plus-event-description.pipe';
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {NULL_DATE} from "../../../_pipes/date-year-range.pipe";
import {
  KavitaPlusAuditEventTypeIconComponent
} from "../../../shared/_components/kavitaplus-event-type-icon/kavita-plus-audit-event-type-icon.component";
import {AuditLogErrorPipe} from "../../../_pipes/audit-log-error.pipe";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {
  ScrobbleProviderImageComponent
} from "../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {UtcToLocalDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {TimeDifferencePipe} from "../../../_pipes/time-difference.pipe";
import {
  ScrobbleProviderTagBadgeComponent
} from "../../../shared/_components/scrobble-provider-tag-badge/scrobble-provider-tag-badge.component";
import {
  MetadataProviderTagBadgeComponent
} from "../../../shared/_components/metadata-provider-tag-badge/metadata-provider-tag-badge.component";

@Component({
  selector: 'app-kavitaplus-tooltip',
  templateUrl: './kavitaplus-tooltip.component.html',
  styleUrls: ['./kavitaplus-tooltip.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, KavitaPlusEventTypePipe, KavitaPlusEventDescriptionPipe, UtcToLocalTimePipe,
    KavitaPlusAuditEventTypeIconComponent, AuditLogErrorPipe, ScrobbleProviderImageComponent, ScrobbleProviderNamePipe,
    UtcToLocalDatePipe, TimeDifferencePipe, ScrobbleProviderTagBadgeComponent, MetadataProviderTagBadgeComponent],
})
export class KavitaplusTooltipComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
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
  totalCount = computed(() => this.seriesInfo()?.recentEvents.length ?? 0);
  metadataCount = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Metadata).length ?? 0);
  scrobbleCount = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Scrobble).length ?? 0);
  matchCount = computed(() => this.seriesInfo()?.recentEvents.filter(e => e.category === KavitaPlusAuditCategory.Match).length ?? 0);

  ngOnInit() {
    this.auditService.getSeriesInfo(this.seriesId()).subscribe({
      next: info => { this.seriesInfo.set(info); this.isLoading.set(false); },
      error: ()   => this.isLoading.set(false),
    });
  }

  setFilter(cat: KavitaPlusAuditCategory | null) {
    this.categoryFilter.set(cat);
  }

  navigateAndClose(commands: unknown[], extras?: NavigationExtras) {
    this.activeOffcanvas?.close();
    this.router.navigate(commands, extras);
  }

  categoryColorClass(category: KavitaPlusAuditCategory): string {
    switch (category) {
      case KavitaPlusAuditCategory.Match:
        return 'match';
      case KavitaPlusAuditCategory.Scrobble:
        return 'scrobble';
      case KavitaPlusAuditCategory.Sync:
        return 'sync';
      default:
        return 'metadata';
    }
  }

  protected readonly AuditCategory = KavitaPlusAuditCategory;
  protected readonly NULL_DATE = NULL_DATE;
  protected readonly SettingsTabId = SettingsTabId;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
