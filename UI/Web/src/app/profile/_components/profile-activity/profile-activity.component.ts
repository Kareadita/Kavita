import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal
} from '@angular/core';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {combineLatest, distinctUntilChanged, filter, tap} from 'rxjs';
import {MemberInfo} from '../../../_models/user/member-info';
import {TranslocoDirective} from '@jsverse/transloco';
import {StatisticsService} from '../../../_services/statistics.service';
import {ReadingHistoryItem} from '../../../_models/stats/reading-history-item';
import {DOCUMENT, TitleCasePipe} from '@angular/common';
import {StatsFilter} from '../../../statistics/_models/stats-filter';
import {
  LibraryAndTimeSelectorComponent
} from '../../../statistics/_components/library-and-time-selector/library-and-time-selector.component';
import {ImageService} from '../../../_services/image.service';
import {Pagination} from '../../../_models/pagination';
import {ReadingHistoryViewerComponent} from "src/app/shared/reading-history-viewer/reading-history-viewer.component";


@Component({
  selector: 'app-profile-activity',
  imports: [
    TranslocoDirective,
    LibraryAndTimeSelectorComponent,
    TitleCasePipe,
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

  protected readonly pageSize = 10;

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
        this.loadPage(1, this.pageSize);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  private loadPage(page: number, pageSize: number): void {
    const f = this.filter();
    const memberId = this.memberInfo()?.id;

    if (!f || !memberId) return;

    this.isLoading.set(true);

    this.statsService.getReadingHistory(f, memberId, page, pageSize)
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

  protected onPageChange(page: number, pageSize: number, scroll: boolean): void {
    if (page === this.currentPage() || this.isLoading()) return;

    this.loadPage(page, pageSize);
    if (scroll) {
      this.document.querySelector('.activity-list')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  updateFilter(event: StatsFilter): void {
    this.filter.set(event);
  }
}
