import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {ActiveUserCardComponent} from "../active-user-card/active-user-card.component";
import {StatsFilter} from "../../_models/stats-filter";

export type TimeFrame = 'week' | 'month' | 'year' | 'allTime';

@Component({
  selector: 'app-most-active-users',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ActiveUserCardComponent
  ],
  templateUrl: './most-active-users.component.html',
  styleUrl: './most-active-users.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MostActiveUsersComponent {

  private readonly statsService = inject(StatisticsService);

  statsFilter = input.required<StatsFilter>();
  timeFrame = input<TimeFrame>('week');
  usersResource = this.statsService.getMostActiveUsers(() => this.statsFilter());

  timeFrameLabel = computed(() => {
    const filter = this.statsFilter();
    if (!filter) return 'overall';

    const { startDate, endDate } = filter.timeFilter;
    if (!startDate || !endDate) return 'overall';

    const timeFrame = this.detectTimeFrame(startDate, endDate);

    const labels: Record<TimeFrame, string> = {
      week: 'this week',
      month: 'this month',
      year: 'this year',
      allTime: 'overall'
    };

    return labels[timeFrame];
  });

  private detectTimeFrame(startDate: Date, endDate: Date): TimeFrame {
    const now = new Date();
    const startOfWeek = this.getStartOfWeek(now);
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    const startOfYear = new Date(now.getFullYear(), 0, 1);

    // Check if dates match "this week"
    if (this.isSameDay(startDate, startOfWeek) && this.isSameDay(endDate, now)) {
      return 'week';
    }

    // Check if dates match "this month"
    if (this.isSameDay(startDate, startOfMonth) && this.isSameDay(endDate, now)) {
      return 'month';
    }

    // Check if dates match "this year"
    if (this.isSameDay(startDate, startOfYear) && this.isSameDay(endDate, now)) {
      return 'year';
    }

    return 'allTime';
  }

  private getStartOfWeek(date: Date): Date {
    const d = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1); // Monday start
    return new Date(d.setDate(diff));
  }

  private isSameDay(a: Date, b: Date): boolean {
    return a.getFullYear() === b.getFullYear()
      && a.getMonth() === b.getMonth()
      && a.getDate() === b.getDate();
  }

}
