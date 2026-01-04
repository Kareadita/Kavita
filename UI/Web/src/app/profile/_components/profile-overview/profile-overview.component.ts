import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {MemberInfo} from "../../../_models/user/member-info";
import {AsyncPipe} from "@angular/common";
import {
  CarouselReelComponent,
  NextPageLoader
} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {map, Observable} from "rxjs";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesService} from "../../../_services/series.service";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {SortField} from "../../../_models/metadata/series-filter";
import {QueryContext} from "../../../_models/metadata/v2/query-context";

type OverviewStream = {
  title: string;
  api: Observable<any[]>;
  nextPageLoader: NextPageLoader;
}

const JustFinishedReadingFilter = {
  limitTo: 20,
  offset: 0,
  combination: FilterCombination.And,
  statements: [
    {
      field: FilterField.ReadProgress,
      comparison: FilterComparison.GreaterThanEqual,
      value: '100'
    } as FilterStatement
  ],
  name: translate('profile-overview.just-finished-reading'),
  sortOptions: {
    sortField: SortField.ReadProgress,
    isAscending: false
  }
} as FilterV2;

@Component({
  selector: 'app-profile-overview',
  imports: [
    AsyncPipe,
    CarouselReelComponent,
    SeriesCardComponent,
    TranslocoDirective
  ],
  templateUrl: './profile-overview.component.html',
  styleUrl: './profile-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileOverviewComponent {

  private readonly seriesService = inject(SeriesService);

  memberInfo = input.required<MemberInfo>();

  streams = computed<OverviewStream[]>(() => {
    const memberId = this.memberInfo().id;

    return [
      {
        title: translate('profile-overview.currently-reading'),
        api: this.seriesService
          .getCurrentlyReading(memberId, 0, 20)
          .pipe(map(pr => pr.result)),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getCurrentlyReading(memberId, pageNum, pageSize),
      },
      {
        title: translate('profile-overview.want-to-read'),
        api: this.seriesService
          .getWantToRead(0, 20, undefined, memberId)
          .pipe(map(pr => pr.result)),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getWantToRead(pageNum, pageSize, undefined, memberId),
      },
      {
        title: translate('profile-overview.just-finished-reading'),
        api: this.seriesService
          .getAllSeriesV2(0, 20, JustFinishedReadingFilter, QueryContext.None, memberId)
          .pipe(map(pr => pr.result)),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getAllSeriesV2(pageNum, pageSize, JustFinishedReadingFilter, QueryContext.None, memberId),
      }
    ];
  });

}
