import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusAuditService} from '../../_services/kavitaplus-audit.service';
import {KavitaPlusAuditEntry} from '../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditStats} from '../../_models/kavitaplus/kavita-plus-audit-stats';
import {KavitaPlusAuditFilter} from '../../_models/kavitaplus/kavita-plus-audit-filter';
import {KavitaPlusAuditCategory} from '../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {allAuditStatuses, AuditStatus} from '../../_models/kavitaplus/audit-status.enum';
import {AuditSubjectType} from '../../_models/kavitaplus/audit-subject-type.enum';
import {KavitaplusTimelineComponent} from '../../_single-module/kavitaplus-timeline/kavitaplus-timeline.component';
import {
  KavitaplusAuditAccordionItemComponent
} from './kavitaplus-audit-accordion-item/kavitaplus-audit-accordion-item.component';
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {AuditStatusTitlePipe} from "../../_pipes/audit-status-title.pipe";

@Component({
  selector: 'app-manage-kavitaplus-activity',
  imports: [
    TranslocoDirective,
    KavitaplusTimelineComponent,
    KavitaplusAuditAccordionItemComponent,
    DefaultValuePipe,
    AuditStatusTitlePipe,
  ],
  templateUrl: './manage-kavitaplus-activity.component.html',
  styleUrl: './manage-kavitaplus-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageKavitaplusActivityComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
  private readonly destroyRef = inject(DestroyRef);

  stats = signal<KavitaPlusAuditStats | null>(null);
  entries = signal<KavitaPlusAuditEntry[]>([]);
  isLoading = signal(false);
  categoryFilter = signal<KavitaPlusAuditCategory | null>(null);
  statusFilter = signal<AuditStatus | null>(null);
  searchQuery = signal('');

  subjectFilter = signal<AuditSubjectType | null>(null);
  userFilter = signal<number | null>(null);
  timeFrameFilter = signal<'all' | '24h' | '7d' | '30d'>('all');

  matchedPercent = computed(() => {
    const s = this.stats();
    if (!s || s.totalEligibleSeriesCount === 0) return 0;
    return Math.round(s.matchedSeriesCount / s.totalEligibleSeriesCount * 100);
  });

  ngOnInit() {
    this.loadStats();
    this.loadEntries();
  }

  private loadStats() {
    this.auditService.getStats().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: s => this.stats.set(s),
    });
  }

  private loadEntries() {
    this.isLoading.set(true);
    const filter: KavitaPlusAuditFilter = {
      category: this.categoryFilter(),
      status: this.statusFilter(),
      search: this.searchQuery() || null,
    };
    this.auditService.getEntries(filter, 0, 50).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.entries.set(result.result ?? []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  setCategoryFilter(cat: KavitaPlusAuditCategory | null) {
    this.categoryFilter.set(cat);
    this.loadEntries();
  }

  onStatusFilterChange(value: AuditStatus | null) {
    this.statusFilter.set(value === null ? null : Number(value) as AuditStatus);
    this.loadEntries();
  }

  onSearchChange(value: string) {
    this.searchQuery.set(value);
    this.loadEntries();
  }

  protected readonly KavitaPlusAuditCategory = KavitaPlusAuditCategory;
  protected readonly AuditStatus = AuditStatus;
  protected readonly allAuditStatuses = allAuditStatuses;
}
