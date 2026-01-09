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
import {combineLatest, distinctUntilChanged, filter, switchMap, tap} from 'rxjs';
import {MemberInfo} from '../../../_models/user/member-info';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {IPageInfo, VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {StatisticsService} from '../../../_services/statistics.service';
import {ReadingHistoryChapterItem, ReadingHistoryItem} from '../../../_models/stats/reading-history-item';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {DatePipe, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
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

interface HistoryDisplayItem {
  id: string;
  type: 'header' | 'entry';
  date?: Date;
  entry?: ReadingHistoryItem;
  page?: number; // Track which page this item came from
}

// Debug colors for pages
const PAGE_COLORS = [
  'rgba(255, 0, 0, 0.1)',    // Red
  'rgba(0, 255, 0, 0.1)',    // Green
  'rgba(0, 0, 255, 0.1)',    // Blue
  'rgba(255, 255, 0, 0.1)',  // Yellow
  'rgba(255, 0, 255, 0.1)',  // Magenta
  'rgba(0, 255, 255, 0.1)',  // Cyan
  'rgba(255, 128, 0, 0.1)',  // Orange
  'rgba(128, 0, 255, 0.1)',  // Purple
];

@Component({
  selector: 'app-profile-activity',
  imports: [
    TranslocoDirective,
    VirtualScrollerModule,
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

  memberInfo = input.required<MemberInfo>();
  filter = signal<StatsFilter | undefined>(undefined);

  chapterInfoRow = viewChild.required<TemplateRef<any>>('chapterInfoRow');
  readStatsTemplate = viewChild.required<TemplateRef<any>>('readStats');

  private readonly pageSize = 30;
  private readonly fetchThreshold = 0.8; // Fetch more when 80% scrolled

  // Debug mode - set to false for production
  protected readonly debugMode = signal(true);

  // State signals
  // Store entries with their page number for debugging
  protected allEntriesWithPage = signal<{ entry: ReadingHistoryItem; page: number }[]>([]);
  protected pagination = signal<Pagination | null>(null);
  protected isLoading = signal(false);

  // For debugging - track scroll info
  protected lastScrollInfo = signal<IPageInfo | null>(null);

  // Computed signals
  protected hasMore = computed(() => {
    const pag = this.pagination();
    if (!pag) return false;
    return pag.currentPage < pag.totalPages;
  });

  protected currentPage = computed(() => this.pagination()?.currentPage ?? 0);

  protected items = computed<HistoryDisplayItem[]>(() => {
    const entriesWithPage = this.allEntriesWithPage();
    const grouped = new Map<string, { entry: ReadingHistoryItem; page: number }[]>();

    for (const item of entriesWithPage) {
      const dateKey = item.entry.localDate.substring(0, 10);
      if (!grouped.has(dateKey)) {
        grouped.set(dateKey, []);
      }
      grouped.get(dateKey)!.push(item);
    }

    const result: HistoryDisplayItem[] = [];
    const sortedDates = Array.from(grouped.keys()).sort((a, b) => b.localeCompare(a));

    for (const dateKey of sortedDates) {
      const [year, month, day] = dateKey.split('-').map(Number);
      const localDate = new Date(year, month - 1, day);

      // Header gets the page of the first entry in that group
      const firstItemPage = grouped.get(dateKey)![0].page;
      result.push({ id: `header-${dateKey}`, type: 'header', date: localDate, page: firstItemPage });

      for (const item of grouped.get(dateKey)!) {
        const chapterIds = item.entry.chapters.map(c => c.chapterId).join(',');
        result.push({
          id: `entry-${item.entry.sessionId}-${item.entry.seriesId}-${chapterIds}`,
          type: 'entry',
          entry: item.entry,
          page: item.page
        });
      }
    }

    return result;
  });

  constructor() {
    combineLatest([
      toObservable(this.filter),
      toObservable(this.memberInfo)
    ]).pipe(
      filter(([f, m]) => !!f && !!m?.id),
      distinctUntilChanged((prev, curr) =>
        JSON.stringify(prev[0]) === JSON.stringify(curr[0]) && prev[1]?.id === curr[1]?.id
      ),
      tap(() => {
        this.allEntriesWithPage.set([]);
        this.pagination.set(null);
        this.isLoading.set(true);
      }),
      switchMap(([f, m]) =>
        this.statsService.getReadingHistory(f!, m!.id, 1, this.pageSize)
      ),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (result) => {
        this.allEntriesWithPage.set(
          result.result.map(entry => ({ entry, page: 1 }))
        );
        this.pagination.set(result.pagination);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load reading history', err);
        this.isLoading.set(false);
      }
    });
  }

  protected onScroll(event: IPageInfo): void {
    this.lastScrollInfo.set(event);

    const items = this.items();
    if (items.length === 0) return;

    // Calculate remaining items to scroll
    const remainingItems = items.length - event.endIndex - 1;

    // Trigger when we're within ~20% of the end OR within 15 items
    const percentageRemaining = remainingItems / items.length;
    const shouldLoad = percentageRemaining <= (1 - this.fetchThreshold) || remainingItems <= 15;

    if (shouldLoad && this.hasMore() && !this.isLoading()) {
      if (this.debugMode()) {
        console.log(`Loading next page: ${remainingItems} items remaining (${(percentageRemaining * 100).toFixed(1)}%)`);
      }
      this.loadNextPage();
    }
  }

  private loadNextPage(): void {
    if (this.isLoading() || !this.hasMore()) return;

    const f = this.filter();
    const memberId = this.memberInfo()?.id;
    const nextPage = this.currentPage() + 1;

    if (!f || !memberId) return;

    this.isLoading.set(true);

    this.statsService.getReadingHistory(f, memberId, nextPage, this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const newEntries = result.result.map(entry => ({ entry, page: nextPage }));
          this.allEntriesWithPage.update(current => [...current, ...newEntries]);
          this.pagination.set(result.pagination);
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load more reading history', err);
          this.isLoading.set(false);
        }
      });
  }

  protected getPageColor(page: number | undefined): string {
    if (!this.debugMode() || page === undefined) return 'transparent';
    return PAGE_COLORS[(page - 1) % PAGE_COLORS.length];
  }

  protected formatProgress(entry: ReadingHistoryItem): string {
    return `${entry.pagesRead}/${entry.totalPages}`;
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
