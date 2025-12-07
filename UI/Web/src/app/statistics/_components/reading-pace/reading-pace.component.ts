import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {StatsFilter} from "../../_models/stats-filter";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";

export interface ReadingPace {
  hoursRead: number;
  pagesRead: number;
  wordsRead: number;
  booksRead: number;
  comicsRead: number;
  daysInRange: number;
}

@Component({
  selector: 'app-reading-pace',
  imports: [
    TranslocoDirective,
    CompactNumberPipe,
    UtcToLocalTimePipe
  ],
  templateUrl: './reading-pace.component.html',
  styleUrl: './reading-pace.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadingPaceComponent {
  private readonly statsService = inject(StatisticsService);

  private readonly compactNumberPipe = new CompactNumberPipe();

  userName = input.required<string>();
  userId = input.required<number>();
  year = input.required<number>();
  filter = input.required<StatsFilter>();

  readingPace = this.statsService.getReadingPaceResource(
    () => this.filter(),
    () => this.userId(),
    () => this.year(),
  );

  stats = computed(() => {
    if (!this.readingPace.hasValue()) {
      return null;
    }

    const stats = this.readingPace.value()!;

    if (stats.booksRead === 0) {
      return null;
    }

    return stats;
  });

  // Calculate pace in days - books per day inverted
  paceInDays = computed(() => {
    const stats = this.stats();
    if (stats == null) return '∞';

    const booksRead = stats.booksRead;
    const days = stats.daysInRange;

    if (booksRead === 0) return '∞';

    return (days / booksRead).toFixed(2);
  });

  // Hours calculations
  hoursPerYear = computed(() => this.projectAnnually(this.stats()?.hoursRead, this.stats()?.daysInRange));
  hoursPerMonth = computed(() => this.projectMonthly(this.stats()?.hoursRead, this.stats()?.daysInRange));
  hoursPerDay = computed(() => this.projectDaily(this.stats()?.hoursRead, this.stats()?.daysInRange));

  // Pages calculations
  pagesPerYear = computed(() => this.projectAnnually(this.stats()?.pagesRead, this.stats()?.daysInRange));
  pagesPerMonth = computed(() => this.projectMonthly(this.stats()?.pagesRead, this.stats()?.daysInRange));
  pagesPerDay = computed(() => this.projectDaily(this.stats()?.pagesRead, this.stats()?.daysInRange));

  // Words calculations
  wordsPerYear = computed(() => this.projectAnnually(this.stats()?.wordsRead, this.stats()?.daysInRange));
  wordsPerMonth = computed(() => this.projectMonthly(this.stats()?.wordsRead, this.stats()?.daysInRange));
  wordsPerDay = computed(() => this.projectDaily(this.stats()?.wordsRead, this.stats()?.daysInRange));

  // Comics calculations
  comicsPerYear = computed(() => this.projectAnnually(this.stats()?.comicsRead, this.stats()?.daysInRange));
  comicsPerMonth = computed(() => this.projectMonthly(this.stats()?.comicsRead, this.stats()?.daysInRange));
  comicsPerDay = computed(() => this.projectDaily(this.stats()?.comicsRead, this.stats()?.daysInRange));

  // Books calculations
  booksPerYear = computed(() => this.projectAnnually(this.stats()?.booksRead, this.stats()?.daysInRange));
  booksPerMonth = computed(() => this.projectMonthly(this.stats()?.booksRead, this.stats()?.daysInRange));
  booksPerDay = computed(() => this.projectDaily(this.stats()?.booksRead, this.stats()?.daysInRange));

  private projectAnnually(value: number | undefined, daysInRange: number | undefined) {
    if (value === undefined || daysInRange === undefined || daysInRange === 0) return '0';
    const projected = (value / daysInRange) * 365;

    return this.formatNumber(projected);
  }

  private projectMonthly(value: number | undefined, daysInRange: number | undefined) {
    if (value === undefined || daysInRange === undefined || daysInRange === 0) return '0';
    const projected = (value / daysInRange) * 30;
    return this.formatNumber(projected);
  }

  private projectDaily(value: number | undefined, daysInRange: number | undefined) {

    if (value === undefined || daysInRange === undefined || daysInRange === 0) return '0';
    const projected = value / daysInRange;
    return this.formatNumber(projected);
  }


  private formatNumber(value: number): string {
    if (value === 0) return '0';

    if (value >= 1000) {
      return this.compactNumberPipe.transform(Math.round(value));
    } else if (value >= 1) {
      return value.toFixed(1);
    } else {
      return value.toFixed(1);
    }
  }
}
