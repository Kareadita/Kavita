import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaplusTimelineComponent} from "../../../_single-module/kavitaplus-timeline/kavitaplus-timeline.component";
import {
  KavitaPlusAuditEntryComponent
} from "../kavitaplus-audit-entry/kavita-plus-audit-entry.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {AuditStatusTitlePipe} from "../../../_pipes/audit-status-title.pipe";
import {AuditSubjectTitlePipe} from "../../../_pipes/audit-subject-title.pipe";
import {KavitaPlusAuditService} from "../../../_services/kavitaplus-audit.service";
import {MemberService} from "../../../_services/member.service";
import {KavitaPlusAuditStats} from "../../../_models/kavitaplus/kavita-plus-audit-stats";
import {KavitaPlusAuditEntry} from "../../../_models/kavitaplus/kavita-plus-audit-entry";
import {allAuditStatuses, AuditStatus} from "../../../_models/kavitaplus/audit-status.enum";
import {allAuditSubjectTypes, AuditSubjectType} from "../../../_models/kavitaplus/audit-subject-type.enum";
import {Member} from "../../../_models/auth/member";
import {Pagination} from "../../../_models/pagination";
import {KavitaPlusAuditCategory} from "../../../_models/kavitaplus/kavita-plus-audit-category.enum";
import {KavitaPlusAuditFilter} from "../../../_models/kavitaplus/kavita-plus-audit-filter";
import {SearchInputComponent} from "../../../shared/_components/search-input/search-input.component";

@Component({
  selector: 'app-manage-kavitaplus-activity',
  imports: [
    TranslocoDirective,
    KavitaplusTimelineComponent,
    KavitaPlusAuditEntryComponent,
    DefaultValuePipe,
    AuditStatusTitlePipe,
    AuditSubjectTitlePipe,
    SearchInputComponent,
  ],
  templateUrl: './manage-kavitaplus-activity.component.html',
  styleUrl: './manage-kavitaplus-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageKavitaplusActivityComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly memberService = inject(MemberService);
  private readonly PAGE_SIZE = 50;

  stats = signal<KavitaPlusAuditStats | null>(null);
  entries = signal<KavitaPlusAuditEntry[]>([]);
  isLoading = signal(false);
  isLoadingMore = signal(false);
  categoryFilter = signal<KavitaPlusAuditCategory | null>(null);
  statusFilter = signal<AuditStatus | null>(null);
  searchQuery = signal('');

  subjectFilter = signal<AuditSubjectType | null>(null);
  userFilter = signal<number | null>(null);
  timeFrameFilter = signal<'all' | '24h' | '7d' | '30d'>('7d');

  members = signal<Member[]>([]);
  currentPage = signal(0);
  pagination = signal<Pagination | null>(null);

  matchedPercent = computed(() => {
    const s = this.stats();
    if (!s || s.totalEligibleSeriesCount === 0) return 0;
    return Math.round(s.matchedSeriesCount / s.totalEligibleSeriesCount * 100);
  });

  hasMore = computed(() => {
    const p = this.pagination();
    return p != null && p.currentPage < p.totalPages - 1;
  });

  ngOnInit() {
    this.loadStats();
    this.loadEntries();

    this.memberService.getMembers(false).subscribe(members => {
      this.members.set(members);
    })
  }

  private loadStats() {
    this.auditService.getStats().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => this.stats.set(s),
    });
  }

  private timeFrameToFromUtc(): string | null {
    const tf = this.timeFrameFilter();
    if (tf === 'all') return null;
    const msMap = { '24h': 86_400_000, '7d': 604_800_000, '30d': 2_592_000_000 };
    return new Date(Date.now() - msMap[tf]).toISOString();
  }

  private loadEntries(reset = true) {
    if (reset) {
      this.currentPage.set(0);
      this.entries.set([]);
      this.isLoading.set(true);
    } else {
      this.isLoadingMore.set(true);
    }

    const filter: KavitaPlusAuditFilter = {
      category: this.categoryFilter(),
      status: this.statusFilter(),
      search: this.searchQuery() || null,
      subjectType: this.subjectFilter() || null,
      fromUtc: this.timeFrameToFromUtc(),
      userId: this.userFilter() || null,
    };

    this.auditService.getEntries(filter, this.currentPage(), this.PAGE_SIZE).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
    this.loadEntries(false);
  }

  setCategoryFilter(cat: KavitaPlusAuditCategory | null) {
    this.categoryFilter.set(cat);
    this.loadEntries();
  }

  onStatusFilterChange(value: string) {
    const num = Number(value);
    this.statusFilter.set(isNaN(num) ? null : num as AuditStatus);
    this.loadEntries();
  }

  onSubjectFilterChange(value: string) {
    const num = Number(value);
    this.subjectFilter.set(isNaN(num) ? null : num as AuditSubjectType);
    this.loadEntries();
  }

  onTimeFilterChange(value: string) {
    this.timeFrameFilter.set(value as 'all' | '24h' | '7d' | '30d');
    this.loadEntries();
  }

  onUserFilterChange(value: number) {
    this.userFilter.set(value < 0 ? null : Number(value));
    this.loadEntries();
  }

  onSearchChange(value: string) {
    this.searchQuery.set(value);
    this.loadEntries();
  }


  protected readonly KavitaPlusAuditCategory = KavitaPlusAuditCategory;
  protected readonly allAuditStatuses = allAuditStatuses;
  protected readonly allAuditSubjectTypes = allAuditSubjectTypes;
}

