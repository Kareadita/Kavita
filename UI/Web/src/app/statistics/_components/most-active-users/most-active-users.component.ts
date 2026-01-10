import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {ActiveUserCardComponent} from "../active-user-card/active-user-card.component";
import {StatsFilter} from "../../_models/stats-filter";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

export type TimeFrame = 'week' | 'month' | 'year' | 'allTime';

@Component({
  selector: 'app-most-active-users',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ActiveUserCardComponent,
    StatsNoDataComponent
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
    if (!filter) return translate('time-frame-label.overall');

    const { startDate, endDate } = filter.timeFilter;
    if (!startDate || !endDate) return translate('time-frame-label.overall');

    const timeFrame = this.detectTimeFrame(startDate, endDate);

    const labels: Record<TimeFrame, string> = {
      week: translate('time-frame-label.week'),
      month: translate('time-frame-label.month'),
      year: translate('time-frame-label.year'),
      allTime: translate('time-frame-label.overall')
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
