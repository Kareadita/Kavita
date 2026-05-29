import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TranslocoDirective} from '@jsverse/transloco';
import {NgbNav, NgbNavItem, NgbNavLink} from '@ng-bootstrap/ng-bootstrap';
import {KavitaPlusAuditService} from '../../_services/kavitaplus-audit.service';
import {ScrobbleProvider, ScrobblingService, UserScrobbleProvider} from '../../_services/scrobbling.service';
import {KavitaPlusAuditEntry} from '../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {AuditStatus} from '../../_models/kavitaplus/audit-status.enum';
import {KavitaplusTimelineComponent} from '../../_single-module/kavitaplus-timeline/kavitaplus-timeline.component';
import {
  KavitaPlusAuditEntryComponent
} from '../../admin/kavita-plus/kavitaplus-audit-entry/kavita-plus-audit-entry.component';
import {ScrobbleAccountCardComponent} from '../scrobble-account-card/scrobble-account-card.component';
import {KavitaPlusEventType} from "../../_models/kavitaplus/kavita-plus-event-type.enum";
import {Tabs} from "../../_models/tabs";
import {TabTitlePipe} from "../../_pipes/tab-title.pipe";
import {Pagination} from '../../_models/pagination';

@Component({
  selector: 'app-kavitaplus-activity',
  templateUrl: './kavitaplus-activity.component.html',
  styleUrls: ['./kavitaplus-activity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, NgbNav, NgbNavItem, NgbNavLink, KavitaplusTimelineComponent, KavitaPlusAuditEntryComponent, ScrobbleAccountCardComponent, TabTitlePipe],
})
export class KavitaplusActivityComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly PAGE_SIZE = 50;

  entries = signal<KavitaPlusAuditEntry[]>([]);
  isLoading = signal(true);
  isLoadingMore = signal(false);
  activeTab = signal<Tabs>(Tabs.All);
  scrobblingProviders = signal<UserScrobbleProvider[]>([]);
  currentPage = signal(0);
  pagination = signal<Pagination | null>(null);

  hasMore = computed(() => {
    const p = this.pagination();
    return p != null && p.currentPage < p.totalPages - 1;
  });

  allCount      = computed(() => this.entries().length);
  scrobbleCount = computed(() => this.entries().filter(e => e.category === KavitaPlusAuditCategory.Scrobble && ![KavitaPlusEventType.ScrobbleHoldAdded, KavitaPlusEventType.ScrobbleHoldRemoved].includes(e.eventType)).length);
  failedCount   = computed(() => this.entries().filter(e => e.status === AuditStatus.Failure).length);
  myChangesCount = computed(() => this.entries().filter(e => e.userId != null).length);
  scrobbleHoldsCount = computed(() => this.entries().filter(e => e.category === KavitaPlusAuditCategory.Scrobble && [KavitaPlusEventType.ScrobbleHoldAdded, KavitaPlusEventType.ScrobbleHoldRemoved].includes(e.eventType)).length);

  filteredEntries = computed(() => {
    const tab = this.activeTab();
    const all = this.entries();
    if (tab === Tabs.Scrobbles) return all.filter(e => e.category === KavitaPlusAuditCategory.Scrobble && ![KavitaPlusEventType.ScrobbleHoldAdded, KavitaPlusEventType.ScrobbleHoldRemoved].includes(e.eventType));
    if (tab === Tabs.Failed)    return all.filter(e => e.status === AuditStatus.Failure);
    if (tab === Tabs.MyChanges) return all.filter(e => e.userId != null);
    if (tab === Tabs.ScrobbleHolds) return all.filter(e => e.category === KavitaPlusAuditCategory.Scrobble && [KavitaPlusEventType.ScrobbleHoldAdded, KavitaPlusEventType.ScrobbleHoldRemoved].includes(e.eventType));
    return all;
  });

  ngOnInit() {
    this.loadData();

    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  loadData(reset = true) {
    if (reset) {
      this.currentPage.set(0);
      this.entries.set([]);
      this.isLoading.set(true);
    } else {
      this.isLoadingMore.set(true);
    }
    this.auditService.getMyActivity({}, this.currentPage(), this.PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.pagination.set(result.pagination);
          if (reset) {
            this.entries.set(result.result ?? []);
            this.isLoading.set(false);
          } else {
            this.entries.update(prev => [...prev, ...(result.result ?? [])]);
            this.isLoadingMore.set(false);
          }
        },
        error: () => {
          this.isLoading.set(false);
          this.isLoadingMore.set(false);
        },
      });
  }


  loadMore() {
    this.currentPage.update(p => p + 1);
    this.loadData(false);
  }

  retryScrobbleEvent(event: KavitaPlusAuditEntry) {
    this.scrobblingService.retryScrobbleEvent(event).subscribe((success) => {
      if (!success) return;
      this.loadData();
    });
  }

  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly Tabs = Tabs;
}
