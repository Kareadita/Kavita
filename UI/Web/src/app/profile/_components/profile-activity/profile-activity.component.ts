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
import {DOCUMENT, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
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
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {ReadingHistoryViewerComponent} from "src/app/shared/reading-history-viewer/reading-history-viewer.component";


@Component({
  selector: 'app-profile-activity',
  imports: [
    TranslocoDirective,
    LoadingComponent,
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
    UtcToLocalTimePipe,
    ReadingHistoryViewerComponent,
  ],
  templateUrl: './profile-activity.component.html',
  styleUrl: './profile-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileActivityComponent {

  private readonly statsService = inject(StatisticsService);
  protected readonly imageService = inject(ImageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);

  memberInfo = input.required<MemberInfo>();
  filter = signal<StatsFilter | undefined>(undefined);

  protected readonly pageSize = 30;

  protected currentEntries = signal<ReadingHistoryItem[]>([]);
  protected pagination = signal<Pagination | null>(null);
  protected isLoading = signal(false);
  protected currentPage = signal(1);

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

  protected onPageChange(page: number, scroll: boolean): void {
    if (page === this.currentPage() || this.isLoading()) return;

    this.loadPage(page);
    if (scroll) {
      this.document.querySelector('.activity-list')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  updateFilter(event: StatsFilter): void {
    this.filter.set(event);
  }
}
