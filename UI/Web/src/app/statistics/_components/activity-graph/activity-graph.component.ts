import {
  ChangeDetectionStrategy,
  Component,
  computed,
  CUSTOM_ELEMENTS_SCHEMA,
  effect,
  inject,
  input,
  model
} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {AccountService} from "../../../_services/account.service";
import {DatePipe, DecimalPipe} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {MonthLabelPipe} from "../../../_pipes/month-label.pipe";
import {DayLabelPipe} from "../../../_pipes/day-label.pipe";
import {UtcToLocaleDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {OrdinalDatePipe} from "../../../_pipes/ordinal-date.pipe";


export interface ActivityGraphData {
  [date: string]: ActivityGraphDataEntry;
}

export interface ActivityGraphDataEntry {
  date: string;
  extraData?: {
    totalTimeReadingSeconds: number;
    totalPages: number;
    totalWords: number;
    totalChaptersFullyRead: number;
  };
}

interface DayCell {
  date: string;
  level: number;
  extraData?: ActivityGraphDataEntry['extraData'];
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
    UtcToLocaleDatePipe,
    OrdinalDatePipe,
    DatePipe
  ],
  templateUrl: './activity-graph.component.html',
  styleUrl: './activity-graph.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActivityGraphComponent {

  private readonly statsService = inject(StatisticsService);
  private readonly accountService = inject(AccountService);

  year = input<number>(new Date().getFullYear());

  data = model<ActivityGraphData>({});

  // Computed values for the grid
  weeks = computed(() => this.generateWeeks());
  months = computed(() => this.generateMonthLabels());

  protected readonly dayLabels = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];


  constructor() {
    effect(() => {
      const userId = this.accountService.currentUserSignal()?.id;
      if (userId) {
        this.loadActivityData(userId, this.year());
      }
    });
  }

  private loadActivityData(userId: number, year: number): void {
    this.statsService.getReadingActivity(userId, year).subscribe(res => {
      this.data.set(res);
    });
  }

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
            date: dateStr,
            level: this.getActivityLevel(entry),
            extraData: entry?.extraData,
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
    const year = this.year();
    const months: Array<{ label: string; colSpan: number }> = [];

    const monthLabelPipe = new MonthLabelPipe();

    for (let month = 0; month < 12; month++) {
      const firstDay = new Date(year, month, 1);
      const lastDay = new Date(year, month + 1, 0);

      // Calculate how many weeks this month spans
      const firstWeek = this.getWeekNumber(firstDay);
      const lastWeek = this.getWeekNumber(lastDay);
      const colSpan = lastWeek - firstWeek + 1;

      if (colSpan > 0) {
        months.push({
          label: monthLabelPipe.transform(month + 1, true),
          colSpan: Math.min(colSpan, 5)
        });
      }
    }

    return months;
  }

  private getWeekNumber(date: Date): number {
    const year = this.year();
    const startOfYear = new Date(year, 0, 1);
    const startDay = startOfYear.getDay();

    // Adjust to start of week
    const adjustedStart = new Date(startOfYear);
    if (startDay !== 0) {
      adjustedStart.setDate(adjustedStart.getDate() - startDay);
    }

    const diff = date.getTime() - adjustedStart.getTime();
    return Math.floor(diff / (7 * 24 * 60 * 60 * 1000));
  }


  private getActivityLevel(entry: ActivityGraphDataEntry | undefined): number {
    if (!entry || !entry.extraData) return 0;

    const { totalTimeReadingSeconds, totalPages } = entry.extraData;

    // Define thresholds for activity levels
    if (totalTimeReadingSeconds === 0 && totalPages === 0) return 0;
    if (totalTimeReadingSeconds < 900) return 1; // Less than 15 minutes
    if (totalTimeReadingSeconds < 1800) return 2; // Less than 30 minutes
    if (totalTimeReadingSeconds < 3600) return 3; // Less than 1 hour
    return 4; // 1 hour or more
  }

  private formatDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }


  formatTime(seconds: number): string {
    if (seconds < 60) return `${seconds} seconds`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)} minutes`;
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours} hours`;
  }

  getLevelClass(level: number): string {
    return `activity-level-${level}`;
  }

  getLevelDescription(level: number): string {
    const descriptions = [
      'activity-graph.no-activity',
      'activity-graph.low-activity',
      'activity-graph.moderate-activity',
      'activity-graph.good-activity',
      'activity-graph.high-activity'
    ];

    return translate(descriptions[level]) || 'activity-graph.no-activity';
  }

}
