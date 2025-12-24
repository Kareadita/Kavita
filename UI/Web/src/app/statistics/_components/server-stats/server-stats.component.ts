import {ChangeDetectionStrategy, Component, computed, effect, inject, signal} from '@angular/core';
import {Router} from '@angular/router';
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from '@ng-bootstrap/ng-bootstrap';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {Series} from 'src/app/_models/series';
import {ImageService} from 'src/app/_services/image.service';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {StatListItem} from '../stat-list/stat-list.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {ReactiveFormsModule} from "@angular/forms";
import {StatsFilter} from "../../_models/stats-filter";
import {Person} from "../../../_models/metadata/person";
import {StatBucket} from "../../_models/stats/stat-bucket";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {forkJoin} from "rxjs";
import {map} from "rxjs/operators";
import {ServerStatsStatsTabComponent} from "../server-stats-stats-tab/server-stats-stats-tab.component";
import {ServerStatsMgmtTabComponent} from "../server-stats-mgmt-tab/server-stats-mgmt-tab.component";

enum TabID {
  Stats = 'stats-tab',
  Management = 'management-tab',
}

@Component({
    selector: 'app-server-stats',
    templateUrl: './server-stats.component.html',
    styleUrls: ['./server-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ReactiveFormsModule, NgbNav, NgbNavContent, NgbNavLink, NgbNavItem, NgbNavOutlet, ServerStatsStatsTabComponent, ServerStatsMgmtTabComponent]
})
export class ServerStatsComponent {
  private readonly statService = inject(StatisticsService);
  private readonly router = inject(Router);
  private readonly imageService = inject(ImageService);
  private readonly utilityService = inject(UtilityService);
  private readonly filterUtilities = inject(FilterUtilitiesService);
  protected readonly accountService = inject(AccountService);

  protected readonly TabID = TabID;

  activeTabId = TabID.Stats;

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
