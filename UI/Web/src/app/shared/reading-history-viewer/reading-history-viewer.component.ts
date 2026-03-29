import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input, output,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {CompactNumberPipe} from "src/app/_pipes/compact-number.pipe";
import {DurationPipe} from "src/app/_pipes/duration.pipe";
import {ImageComponent} from "src/app/shared/image/image.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {UtcToLocalTimePipe} from "src/app/_pipes/utc-to-local-time.pipe";
import {RouterLink} from "@angular/router";
import {NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "src/app/shared/loading/loading.component";
import {MangaFormatPipe} from "src/app/_pipes/manga-format.pipe";
import {NgbPagination, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {StatsNoDataComponent} from "src/app/common/stats-no-data/stats-no-data.component";
import {TagBadgeComponent} from "src/app/shared/tag-badge/tag-badge.component";
import {ImageService} from "src/app/_services/image.service";
import {ReadingHistoryChapterItem, ReadingHistoryItem} from "src/app/_models/stats/reading-history-item";
import {ListSelectModalComponent} from "src/app/shared/_components/list-select-modal/list-select-modal.component";
import {ModalService} from "src/app/_services/modal.service";
import {PaginatedResult, Pagination} from "src/app/_models/pagination";
import {Observable} from "rxjs";

@Component({
  selector: 'app-reading-history-viewer',
  imports: [
    CompactNumberPipe,
    DurationPipe,
    ImageComponent,
    TranslocoDirective,
    UtcToLocalTimePipe,
    RouterLink,
    NgTemplateOutlet,
    LoadingComponent,
    MangaFormatPipe,
    NgbPagination,
    NgbTooltip,
    StatsNoDataComponent,
    TagBadgeComponent
  ],
  templateUrl: './reading-history-viewer.component.html',
  styleUrl: './reading-history-viewer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadingHistoryViewerComponent {

  protected readonly imageService = inject(ImageService);
  private readonly modalService = inject(ModalService);

  chapterInfoRow = viewChild.required<TemplateRef<any>>('chapterInfoRow');
  readStatsTemplate = viewChild.required<TemplateRef<any>>('readStats');

  currentEntries = input.required<ReadingHistoryItem[]>();
  pagination = input.required<Pagination | null>();
  isLoading = input(false);
  currentPage = input.required<number>();
  pageSize = input(30);

  pageChange = output<{page: number, scroll: boolean}>();

  protected readonly totalPages = computed(() => this.pagination()?.totalPages ?? 1);
  protected readonly totalItems = computed(() => this.pagination()?.totalItems ?? 0);

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
    const ref = this.modalService.open(ListSelectModalComponent<{entry: ReadingHistoryItem, chapter: ReadingHistoryChapterItem}>, {
      size: 'lg',
      centered: true
    });

    ref.setInput('title', item.seriesName);
    ref.setInput('showConfirm', false);
    ref.setInput('inputItems', item.chapters.map(c => ({ value: {entry: item, chapter: c}, label: `${c.label}` })));
    ref.setInput('itemTemplate', this.chapterInfoRow());
    ref.setInput('itemsBeforeVirtual', 5);
  }

}
