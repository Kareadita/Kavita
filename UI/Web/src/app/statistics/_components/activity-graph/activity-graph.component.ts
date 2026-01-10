import {ChangeDetectionStrategy, Component, computed, CUSTOM_ELEMENTS_SCHEMA, inject, input} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {DatePipe, DecimalPipe} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {MonthLabelPipe} from "../../../_pipes/month-label.pipe";
import {DayLabelPipe} from "../../../_pipes/day-label.pipe";
import {UtcToLocalDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {OrdinalDatePipe} from "../../../_pipes/ordinal-date.pipe";
import {DurationPipe} from "../../../_pipes/duration.pipe";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {StatsFilter} from "../../_models/stats-filter";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";


export interface ActivityGraphData {
  [date: string]: ActivityGraphDataEntry;
}

export interface ActivityGraphDataEntry {
  date: string;
  totalTimeReadingSeconds: number;
  totalPages: number;
  totalWords: number;
  totalChaptersFullyRead: number;
}

interface DayCell extends ActivityGraphDataEntry {
  date: string;
  level: number;
}

interface WeekRow {
  days: (DayCell | null)[];
}

@Component({
  selector: 'app-activity-graph',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  imports: [
    TranslocoDirective,
    DecimalPipe,
    NgbTooltip,
    DayLabelPipe,
    UtcToLocalDatePipe,
    OrdinalDatePipe,
    DatePipe,
    DurationPipe,
    LoadingComponent,
    CompactNumberPipe
  ],
  templateUrl: './activity-graph.component.html',
  styleUrl: './activity-graph.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActivityGraphComponent {

  private readonly statsService = inject(StatisticsService);

  userId = input.required<number>();
  year = input.required<number>();
  filter = input.required<StatsFilter>();

  protected readonly readingActivityResource = this.statsService.getReadingActivityResource(
    () => this.filter(),
    () => this.userId(),
    () => this.year(),
  );

  data = computed(() => {
    if (this.readingActivityResource.hasValue()) {
      return this.readingActivityResource.value();
    }

    return {};
  });

  protected aggregatedCount = computed(() => Object.values(this.data())
    .filter(entry => new Date(entry.date).getFullYear() == this.year())
    .reduce((prev, cur) =>
      ({totalWords: prev.totalWords + cur.totalWords, totalPages: prev.totalPages + cur.totalPages}),
      {totalWords: 0, totalPages: 0}));

  // Computed values for the grid
  weeks = computed(() => this.generateWeeks());
  months = computed(() => this.generateMonthLabels());

  timeFrameLabel = computed(() => {
    const filter = this.filter();
    const year = this.year();
    if (!filter) return year;

    return year;
  })

  private generateWeeks(): WeekRow[] {
    const year = this.year();
    const startDate = new Date(year, 0, 1); // January 1st
    const endDate = new Date(year, 11, 31); // December 31st

    // Adjust start date to the beginning of the week (Sunday)
    const startDay = startDate.getDay();
    if (startDay !== 0) {
      startDate.setDate(startDate.getDate() - startDay);
    }

    const weeks: WeekRow[] = [];
    const currentDate = new Date(startDate);

    while (currentDate <= endDate || currentDate.getDay() !== 0) {
      const week: (DayCell | null)[] = [];

      for (let i = 0; i < 7; i++) {
        if (currentDate.getFullYear() === year) {
          const dateStr = this.formatDate(currentDate);
          const entry = this.data()[dateStr];

          week.push({
            ...entry,
            date: dateStr,
            level: this.getActivityLevel(entry)
          });
        } else {
          // Outside the year, add null for empty cells
          week.push(null);
        }

        currentDate.setDate(currentDate.getDate() + 1);
      }

      weeks.push({ days: week });
    }

    return weeks;
  }

  private generateMonthLabels(): Array<{ label: string; colSpan: number }> {
    const monthLabelPipe = new MonthLabelPipe();
    const monthLabels: Array<{ label: string; colSpan: number }> = [];

    const startDate = new Date(this.year(), 0, 1);
    const endDate = new Date(this.year(), 11, 31);

    let currentDate = new Date(startDate);

    // Get the first Saturday
    const firstDayOfWeek = currentDate.getDay();
    const daysUntilSaturday = (6 - firstDayOfWeek + 7) % 7;
    currentDate.setDate(currentDate.getDate() + daysUntilSaturday);

    let currentMonth = -1;
    let currentMonthColSpan = 0;

    while (currentDate <= endDate) {
      const month = currentDate.getMonth();

      if (month !== currentMonth) {
        if (currentMonth !== -1) {
          const monthName = monthLabelPipe.transform(currentMonth + 1, true);
          monthLabels.push({
            label: monthName,
            colSpan: currentMonthColSpan
          });
        }

        currentMonth = month;
        currentMonthColSpan = 0;
      }

      currentMonthColSpan++;

      // Jump to next Saturday (7 days later)
      currentDate.setDate(currentDate.getDate() + 7);
    }

    if (currentMonth !== -1) {
      const monthName = monthLabelPipe.transform(currentMonth + 1, true);
      monthLabels.push({
        label: monthName,
        colSpan: currentMonthColSpan
      });
    }

    return monthLabels;
  }


  private getActivityLevel(entry: ActivityGraphDataEntry | undefined): number {
    if (!entry) return 0;

    if (entry.totalTimeReadingSeconds === 0 && entry.totalPages === 0) return 0;
    if (entry.totalTimeReadingSeconds < 15 * 60) return 1; // Less than 15 minutes
    if (entry.totalTimeReadingSeconds < 45 * 60) return 2; // Less than 45 minutes
    if (entry.totalTimeReadingSeconds < 60 * 60) return 3; // Less than 1 hour
    return 4; // 1 hour or more
  }

  private formatDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  getLevelClass(level: number): string {
    return `activity-level-${level}`;
  }

  getLevelDescription(level: number): string {
    const descriptions = [
      'activity-graph.no-activity-alt',
      'activity-graph.low-activity-alt',
      'activity-graph.moderate-activity-alt',
      'activity-graph.good-activity-alt',
      'activity-graph.high-activity-alt'
    ];

    return translate(descriptions[level]) || 'activity-graph.no-activity-alt';
  }

}
