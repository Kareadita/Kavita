import {ChangeDetectionStrategy, Component, computed, effect, inject, input, signal} from '@angular/core';
import {MemberInfo} from "../../../_models/user/member-info";
import {TranslocoDirective} from "@jsverse/transloco";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {StatisticsService} from "../../../_services/statistics.service";
import {ReadingHistoryItem} from "../../../_models/stats/reading-history-item";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {DatePipe, TitleCasePipe} from "@angular/common";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {RouterLink} from "@angular/router";
import {
  LibraryAndTimeSelectorComponent
} from "../../../statistics/_components/library-and-time-selector/library-and-time-selector.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

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
    TitleCasePipe,
    LibraryAndTimeSelectorComponent,
    StatsNoDataComponent
  ],
  templateUrl: './profile-activity.component.html',
  styleUrl: './profile-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileActivityComponent {
  private readonly statsService = inject(StatisticsService);

  memberInfo = input.required<MemberInfo>();
  filter = signal<StatsFilter | undefined>(undefined);

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
      const value = this.historyResource.value();
      console.log('Raw resource value:', value);
      console.log('Type:', typeof value, Array.isArray(value));
    });

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
    return result.length >= this.pageSize;
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
        result.push({ id: `entry-${entry.sessionId}`, type: 'entry', entry });
      }
    }
    console.log(result)
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

  updateFilter(event: StatsFilter) {
    this.filter.set(event);
  }
}
