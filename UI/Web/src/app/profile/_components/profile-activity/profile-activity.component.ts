import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {MemberInfo} from "../../../_models/user/member-info";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {StatisticsService} from "../../../_services/statistics.service";
import {ReadingHistoryChapterItem, ReadingHistoryItem} from "../../../_models/stats/reading-history-item";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {DatePipe, TitleCasePipe} from "@angular/common";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {RouterLink} from "@angular/router";
import {
  LibraryAndTimeSelectorComponent
} from "../../../statistics/_components/library-and-time-selector/library-and-time-selector.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {ImageComponent} from "../../../shared/image/image.component";
import {ImageService} from "../../../_services/image.service";
import {ModalService} from "../../../_services/modal.service";
import {ListSelectModalComponent} from "../../../shared/_components/list-select-modal/list-select-modal.component";

interface HistoryDisplayItem {
  id: string;
  type: 'header' | 'entry';
  date?: Date;
  entry?: ReadingHistoryItem;
}

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
  ],
  templateUrl: './profile-activity.component.html',
  styleUrl: './profile-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileActivityComponent {

  private readonly statsService = inject(StatisticsService);
  protected readonly imageService = inject(ImageService);
  private readonly modalService = inject(ModalService);

  memberInfo = input.required<MemberInfo>();
  filter = signal<StatsFilter | undefined>(undefined);

  chapterInfoRow = viewChild.required<TemplateRef<any>>('chapterInfoRow');
  protected currentPage = signal(1);
  private readonly pageSize = 30;
  protected allEntries = signal<ReadingHistoryItem[]>([]);

  protected historyResource = this.statsService.getReadingHistoryResource(
    () => this.filter(),
    () => this.memberInfo()?.id ?? 0,
    () => this.currentPage(),
    () => this.pageSize
  );

  constructor() {
    effect(() => {
      const result = this.historyResource.value();
      if (result && result.length > 0) {
        if (this.currentPage() === 1) {
          this.allEntries.set(result);
        } else {
          this.allEntries.update(current => [...current, ...result]);
        }
      }
    });
  }

  protected hasMore = computed(() => {
    const result = this.historyResource.value();
    if (!result) return true; // Assume more until proven otherwise

    return result.flatMap(item => item.sessionDataIds).length >= this.pageSize;
  });

  protected items = computed<HistoryDisplayItem[]>(() => {
    const grouped = new Map<string, ReadingHistoryItem[]>();

    for (const entry of this.allEntries()) {
      // localDate is already "2026-01-06T00:00:00", just grab the date part
      const dateKey = entry.localDate.substring(0, 10); // "2026-01-06"
      if (!grouped.has(dateKey)) {
        grouped.set(dateKey, []);
      }
      grouped.get(dateKey)!.push(entry);
    }

    const result: HistoryDisplayItem[] = [];
    const sortedDates = Array.from(grouped.keys()).sort((a, b) => b.localeCompare(a));

    for (const dateKey of sortedDates) {
      // Parse as local date, not UTC
      const [year, month, day] = dateKey.split('-').map(Number);
      const localDate = new Date(year, month - 1, day);

      result.push({ id: `header-${dateKey}`, type: 'header', date: localDate });
      for (const entry of grouped.get(dateKey)!) {
        const chapterIds = entry.chapters.map(c => c.chapterId).join(",")
        result.push({ id: `entry-${entry.sessionId}-${chapterIds}`, type: 'entry', entry });
      }
    }

    return result;
  });

  protected loadMore(): void {
    if (this.historyResource.isLoading() || !this.hasMore()) return;
    this.currentPage.update(p => p + 1);
  }

  protected formatDuration(seconds: number): string {
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return remainingMinutes > 0 ? `${hours}h ${remainingMinutes}m` : `${hours}h`;
  }

  protected formatProgress(entry: ReadingHistoryItem): string {
    if (entry.completed) return 'Completed';
    return `${entry.endPage}/${entry.totalPages}`;
  }

  protected displayInfo(item: ReadingHistoryItem) {
    const [_, component] = this.modalService.open(ListSelectModalComponent<ReadingHistoryChapterItem>);

    component.title.set(translate('profile-activity.chapter-detail-modal-title', {seriesName: item.seriesName}));
    component.showConfirm.set(false);
    component.inputItems.set(item.chapters.map(c => ({value: c, label: `${c.label}`})));
    component.itemTemplate.set(this.chapterInfoRow());
    component.itemsBeforeVirtual.set(5);

  }

  updateFilter(event: StatsFilter) {
    this.filter.set(event);
  }
}
