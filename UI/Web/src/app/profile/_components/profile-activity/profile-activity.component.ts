import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {combineLatest, distinctUntilChanged, filter, tap} from 'rxjs';
import {MemberInfo} from '../../../_models/user/member-info';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {StatisticsService} from '../../../_services/statistics.service';
import {ReadingHistoryChapterItem, ReadingHistoryItem} from '../../../_models/stats/reading-history-item';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {DatePipe, DOCUMENT, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {StatsFilter} from '../../../statistics/_models/stats-filter';
import {RouterLink} from '@angular/router';
import {
  LibraryAndTimeSelectorComponent
} from '../../../statistics/_components/library-and-time-selector/library-and-time-selector.component';
import {StatsNoDataComponent} from '../../../common/stats-no-data/stats-no-data.component';
import {MangaFormatPipe} from '../../../_pipes/manga-format.pipe';
import {TagBadgeComponent} from '../../../shared/tag-badge/tag-badge.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {ImageService} from '../../../_services/image.service';
import {ModalService} from '../../../_services/modal.service';
import {ListSelectModalComponent} from '../../../shared/_components/list-select-modal/list-select-modal.component';
import {CompactNumberPipe} from '../../../_pipes/compact-number.pipe';
import {DurationPipe} from '../../../_pipes/duration.pipe';
import {Pagination} from '../../../_models/pagination';
import {NgbPagination, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';


@Component({
  selector: 'app-profile-activity',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    DatePipe,
    RouterLink,
    LibraryAndTimeSelectorComponent,
    StatsNoDataComponent,
    MangaFormatPipe,
    TagBadgeComponent,
    ImageComponent,
    TitleCasePipe,
    CompactNumberPipe,
    NgTemplateOutlet,
    DurationPipe,
    NgbPagination,
    NgbTooltip,
  ],
  templateUrl: './profile-activity.component.html',
  styleUrl: './profile-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileActivityComponent {

  private readonly statsService = inject(StatisticsService);
  protected readonly imageService = inject(ImageService);
  private readonly modalService = inject(ModalService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);

  memberInfo = input.required<MemberInfo>();
  filter = signal<StatsFilter | undefined>(undefined);

  chapterInfoRow = viewChild.required<TemplateRef<any>>('chapterInfoRow');
  readStatsTemplate = viewChild.required<TemplateRef<any>>('readStats');

  protected readonly pageSize = 30;

  // State signals
  protected currentEntries = signal<ReadingHistoryItem[]>([]);
  protected pagination = signal<Pagination | null>(null);
  protected isLoading = signal(false);
  protected currentPage = signal(1);

  protected totalPages = computed(() => this.pagination()?.totalPages ?? 1);
  protected totalItems = computed(() => this.pagination()?.totalItems ?? 0);

  constructor() {
    // React to filter/member changes - reset to page 1
    combineLatest([
      toObservable(this.filter),
      toObservable(this.memberInfo)
    ]).pipe(
      filter(([f, m]) => !!f && !!m?.id),
      distinctUntilChanged((prev, curr) =>
        JSON.stringify(prev[0]) === JSON.stringify(curr[0]) && prev[1]?.id === curr[1]?.id
      ),
      tap(() => {
        this.currentPage.set(1);
        this.loadPage(1);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  private loadPage(page: number): void {
    const f = this.filter();
    const memberId = this.memberInfo()?.id;

    if (!f || !memberId) return;

    this.isLoading.set(true);

    this.statsService.getReadingHistory(f, memberId, page, this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.currentEntries.set(result.result);
          this.pagination.set(result.pagination);
          this.currentPage.set(page);
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load reading history', err);
          this.isLoading.set(false);
        }
      });
  }

  protected onPageChange(page: number): void {
    if (page === this.currentPage() || this.isLoading()) return;

    this.loadPage(page);
    this.document.querySelector('.activity-list')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected formatProgress(entry: ReadingHistoryItem): string {
    return `${entry.pagesRead}/${entry.totalPages}`;
  }

  /**
   * Returns relative date string for today/yesterday, otherwise formatted date
   */
  protected formatEntryDate(entry: ReadingHistoryItem): string {
    const [year, month, day] = entry.localDate.substring(0, 10).split('-').map(Number);
    const entryDate = new Date(year, month - 1, day);
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    if (entryDate.getTime() === today.getTime()) {
      return translate('profile-activity.today');
    }
    if (entryDate.getTime() === yesterday.getTime()) {
      return translate('profile-activity.yesterday');
    }

    // Format as "Jan 4, 2025"
    return entryDate.toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    });
  }

  protected displayInfo(item: ReadingHistoryItem): void {
    const [_, component] = this.modalService.open(ListSelectModalComponent<ReadingHistoryChapterItem>, {
      size: 'lg',
      centered: true
    });

    component.title.set(translate('profile-activity.chapter-detail-modal-title', { seriesName: item.seriesName }));
    component.showConfirm.set(false);
    component.inputItems.set(item.chapters.map(c => ({ value: c, label: `${c.label}` })));
    component.itemTemplate.set(this.chapterInfoRow());
    component.itemsBeforeVirtual.set(5);
  }

  updateFilter(event: StatsFilter): void {
    this.filter.set(event);
  }
}
