import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {MemberInfo} from "src/app/_models/user/member-info";
import {AsyncPipe} from "@angular/common";
import {
  CarouselReelComponent,
  NextPageLoader
} from "src/app/carousel/_components/carousel-reel/carousel-reel.component";
import {map, Observable, OperatorFunction} from "rxjs";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesService} from "src/app/_services/series.service";
import {FilterV2} from "src/app/_models/metadata/v2/filter-v2";
import {FilterCombination} from "src/app/_models/metadata/v2/filter-combination";
import {FilterStatement} from "src/app/_models/metadata/v2/filter-statement";
import {FilterComparison} from "src/app/_models/metadata/v2/filter-comparison";
import {SeriesFilterField} from "src/app/_models/metadata/v2/series-filter-field";
import {SeriesSortField} from "src/app/_models/metadata/series-filter";
import {QueryContext} from "src/app/_models/metadata/v2/query-context";
import {EntityCardComponent} from "src/app/cards/entity-card/entity-card.component";
import {CardConfigFactory} from "src/app/_services/card-config-factory.service";
import {CardEntityFactory, SeriesCardEntity} from "src/app/_models/card/card-entity";
import {Series} from "src/app/_models/series";
import {FilterEntityType} from "../../../_models/metadata/v2/filter-entity-type";

type OverviewStream = {
  title: string;
  api: Observable<SeriesCardEntity[]>;
  nextPageLoader: NextPageLoader;
}

const JustFinishedReadingFilter = {
  entityType: FilterEntityType.Series,
  limitTo: 20,
  offset: 0,
  combination: FilterCombination.And,
  statements: [
    {
      field: SeriesFilterField.ReadProgress,
      comparison: FilterComparison.GreaterThanEqual,
      value: '100'
    } as FilterStatement
  ],
  name: translate('profile-overview.just-finished-reading'),
  sortOptions: {
    sortField: SeriesSortField.ReadProgress,
    isAscending: false
  }
} as FilterV2;

@Component({
  selector: 'app-profile-overview',
  imports: [
    AsyncPipe,
    CarouselReelComponent,
    TranslocoDirective,
    EntityCardComponent
  ],
  templateUrl: './profile-overview.component.html',
  styleUrl: './profile-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileOverviewComponent {

  private readonly seriesService = inject(SeriesService);
  private readonly cardConfigFactory = inject(CardConfigFactory);

  memberInfo = input.required<MemberInfo>();

  streams = computed<OverviewStream[]>(() => {
    const memberId = this.memberInfo().id;
    const seriesMapper: OperatorFunction<Series[], SeriesCardEntity[]> = map(
      series => series.map(s => CardEntityFactory.series(s))
    );

    return [
      {
        title: translate('profile-overview.currently-reading'),
        api: this.seriesService
          .getCurrentlyReading(memberId, 0, 20)
          .pipe(map(pr => pr.result), seriesMapper),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getCurrentlyReading(memberId, pageNum, pageSize)
          .pipe(map(pr => pr.result), seriesMapper),
      },
      {
        title: translate('profile-overview.want-to-read'),
        api: this.seriesService
          .getWantToRead(0, 20, undefined, memberId)
          .pipe(map(pr => pr.result), seriesMapper),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getWantToRead(pageNum, pageSize, undefined, memberId)
          .pipe(map(pr => pr.result), seriesMapper),
      },
      {
        title: translate('profile-overview.just-finished-reading'),
        api: this.seriesService
          .getAllSeriesV2(0, 20, JustFinishedReadingFilter, QueryContext.None, memberId)
          .pipe(map(pr => pr.result), seriesMapper),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getAllSeriesV2(pageNum, pageSize, JustFinishedReadingFilter, QueryContext.None, memberId)
          .pipe(map(pr => pr.result), seriesMapper),
      }
    ];
  });

  seriesConfig = computed(() => this.cardConfigFactory.forSeries({overrides: {allowSelection: false}}));

}
