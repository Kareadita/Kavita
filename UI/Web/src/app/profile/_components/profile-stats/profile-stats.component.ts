import {ChangeDetectionStrategy, Component, inject, input, signal} from '@angular/core';
import {ActivityGraphComponent} from "../../../statistics/_components/activity-graph/activity-graph.component";
import {
  AvgTimeSpendReadingByHourComponent
} from "../../../statistics/_components/avg-time-spend-reading-by-hour/avg-time-spend-reading-by-hour.component";
import {
  BucketSpreadChartComponent
} from "../../../statistics/_components/bucket-spread-chart/bucket-spread-chart.component";
import {FavoriteAuthorsComponent} from "../../../statistics/_components/favorite-authors/favorite-authors.component";
import {
  LibraryAndTimeSelectorComponent
} from "../../../statistics/_components/library-and-time-selector/library-and-time-selector.component";
import {ProfileStatBarComponent} from "../profile-stat-bar/profile-stat-bar.component";
import {
  ReadingPaceComponent,
  ReadingPaceType
} from "../../../statistics/_components/reading-pace/reading-pace.component";
import {ReadsByMonthComponent} from "../../../statistics/_components/reads-by-month/reads-by-month.component";
import {StringBreakdownComponent} from "../../../statistics/_components/string-breakdown/string-breakdown.component";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {StatisticsService} from "../../../_services/statistics.service";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-profile-stats',
  imports: [
    ActivityGraphComponent,
    AvgTimeSpendReadingByHourComponent,
    BucketSpreadChartComponent,
    FavoriteAuthorsComponent,
    LibraryAndTimeSelectorComponent,
    ProfileStatBarComponent,
    ReadingPaceComponent,
    ReadsByMonthComponent,
    StringBreakdownComponent,
    TranslocoDirective
  ],
  templateUrl: './profile-stats.component.html',
  styleUrl: './profile-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileStatsComponent {

  private readonly statsService = inject(StatisticsService);

  userId = input.required<number>();
  username = input.required<string>();

  protected filter = signal<StatsFilter | undefined>(undefined);
  protected year = signal<number>(new Date().getFullYear());

  protected readonly genreBreakdown = this.statsService.getGenreBreakDownResource(() => this.filter(), () => this.userId());
  protected readonly tagsBreakdown = this.statsService.getTagBreakDownResource(() => this.filter(), () => this.userId());
  protected readonly wordSpreadResource = this.statsService.getWordSpread(() => this.filter(), () => this.userId());
  protected readonly pageSpreadResource = this.statsService.getPageSpread(() => this.filter(), () => this.userId());
  protected readonly readsByMonth = this.statsService.getReadsByMonths(() => this.filter(), () => this.userId());
  protected readonly avgTimeSpendReadingByHour = this.statsService.getAvgTimeSpendReadingByHour(() => this.filter(), () => this.userId());


  protected readonly ReadingPaceType = ReadingPaceType;
}
