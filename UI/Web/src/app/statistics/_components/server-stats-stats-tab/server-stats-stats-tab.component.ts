import {ChangeDetectionStrategy, Component, computed, effect, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {DecimalPipe} from "@angular/common";
import {IconAndTitleComponent} from "../../../shared/icon-and-title/icon-and-title.component";
import {LibraryAndTimeSelectorComponent} from "../library-and-time-selector/library-and-time-selector.component";
import {MostActiveUsersComponent} from "../most-active-users/most-active-users.component";
import {ReadingActivityComponent} from "../reading-activity/reading-activity.component";
import {StatListComponent, StatListItem} from "../stat-list/stat-list.component";
import {TimeDurationPipe} from "../../../_pipes/time-duration.pipe";
import {StatisticsService} from "../../../_services/statistics.service";
import {ImageService} from "../../../_services/image.service";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {AccountService} from "../../../_services/account.service";
import {StatsFilter} from "../../_models/stats-filter";
import {StatBucket} from "../../_models/stats/stat-bucket";
import {Series} from "../../../_models/series";
import {Person} from "../../../_models/metadata/person";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {map} from "rxjs/operators";
import {forkJoin} from "rxjs";

@Component({
  selector: 'app-server-stats-stats-tab',
  imports: [
    TranslocoDirective,
    BytesPipe,
    CompactNumberPipe,
    DecimalPipe,
    IconAndTitleComponent,
    LibraryAndTimeSelectorComponent,
    MostActiveUsersComponent,
    ReadingActivityComponent,
    StatListComponent,
    TimeDurationPipe
  ],
  templateUrl: './server-stats-stats-tab.component.html',
  styleUrl: './server-stats-stats-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServerStatsStatsTabComponent {
  private readonly statService = inject(StatisticsService);
  private readonly imageService = inject(ImageService);
  private readonly filterUtilities = inject(FilterUtilitiesService);
  protected readonly accountService = inject(AccountService);


  userId = computed(() => this.accountService.currentUserSignal()?.id);
  readonly filter = signal<StatsFilter | undefined>(undefined);
  readonly year = signal<number>(new Date().getFullYear());

  readonly releaseYearsResource = this.statService.getPopularDecadesResource();
  readonly releaseYears = computed(() => {
    return (this.releaseYearsResource.value() ?? []).map(r => {
      return {name: `${r.rangeStart}s`, value: r.count, data: r};
    }) as StatListItem[];
  });
  readonly releaseYearsWithUrls = signal<StatListItem[]>([]);
  readonly getDecadeUrl = (item: StatListItem) => {
    const data = item.data as StatBucket & { url?: string };
    return data?.url ?? null;
  }

  readonly statsResource = this.statService.getServerStatisticsResource();

  readonly popularLibrariesResource = this.statService.getPopularLibraries();
  readonly popularLibraries = computed(() => {
    return (this.popularLibrariesResource.value() ?? []).map(r => {
      return {name: r.value.name, value: r.count, data: r.value};
    }) as StatListItem[];
  });

  readonly popularSeriesResource = this.statService.getPopularSeries();
  readonly popularSeries = computed(() => {
    return (this.popularSeriesResource.value() ?? []).map(r => {
      return {name: r.value.name, value: r.count, data: r.value};
    }) as StatListItem[];
  });
  readonly mostPopularSeriesCover = computed(() => {
    const popular = this.popularSeries();
    if (!popular || popular.length === 0) {
      return '';
    }
    return this.imageService.getSeriesCoverImage((popular[0].data as Series).id)
  });
  readonly getSeriesImage = (item: StatListItem) => this.imageService.getSeriesCoverImage((item.data as Series).id);
  readonly getSeriesUrl = (item: StatListItem) => {
    const series = item.data as Series;
    return `/library/${series.libraryId}/series/${series.id}`;
  };


  readonly genresResource = this.statService.getPopularGenresResource();
  readonly popularGenres = computed(() => {
    return (this.genresResource.value() ?? []).map(r => {
      return {name: r.value.title, value: r.count, data: r.value};
    }) as StatListItem[];
  });

  readonly tagsResource = this.statService.getPopularTagsResource();
  readonly popularTags = computed(() => {
    return (this.tagsResource.value() ?? []).map(r => {
      return {name: r.value.title, value: r.count, data: r.value};
    }) as StatListItem[];
  });

  readonly artistResource = this.statService.getPopularArtistsResource();
  readonly popularArtists = computed(() => {
    return (this.artistResource.value() ?? []).map(r => {
      return {name: r.value.name, value: r.count, data: r.value};
    }) as StatListItem[];
  });

  readonly authorsResource = this.statService.getPopularAuthorsResource();
  readonly popularAuthors = computed(() => {
    return (this.authorsResource.value() ?? []).map(r => {
      return {name: r.value.name, value: r.count, data: r.value};
    }) as StatListItem[];
  });

  readonly getPersonUrl = (item: StatListItem) => {
    const person = item.data as Person;
    return `/person/${person.name}`;
  };

  constructor() {
    effect(() => {
      const items = this.releaseYears();
      if (!items.length) {
        this.releaseYearsWithUrls.set([]);
        return;
      }

      // Build all filter encode requests
      const urlRequests = items.map(item => {
        const decade = item.data as StatBucket;
        return this.filterUtilities.encodeFilter({
          statements: [
            {comparison: FilterComparison.GreaterThanEqual, field: FilterField.ReleaseYear, value: decade.rangeStart + ''},
            {comparison: FilterComparison.LessThanEqual, field: FilterField.ReleaseYear, value: decade.rangeEnd + ''},
          ],
          combination: FilterCombination.And,
          limitTo: 0,
          name: `${decade.rangeStart}s`
        }).pipe(
          map(encoded => ({
            ...item,
            data: { ...decade, url: '/all-series?' + encoded }
          }))
        );
      });

      forkJoin(urlRequests).subscribe(resolved => {
        this.releaseYearsWithUrls.set(resolved);
      });
    });
  }
}
