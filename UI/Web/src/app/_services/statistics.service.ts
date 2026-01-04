import {HttpClient, HttpParams, httpResource} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {UserReadStatistics} from '../statistics/_models/user-read-statistics';
import {PublicationStatusPipe} from '../_pipes/publication-status.pipe';
import {asyncScheduler, map} from 'rxjs';
import {FileExtensionBreakdown} from '../statistics/_models/file-breakdown';
import {MostActiveUser} from '../statistics/_models/top-reads';
import {ServerStatistics} from '../statistics/_models/server-statistics';
import {StatCount, StatCountWithFormat} from '../statistics/_models/stat-count';
import {PublicationStatus} from '../_models/metadata/publication-status';
import {MangaFormat} from '../_models/manga-format';
import {TranslocoService} from "@jsverse/transloco";
import {throttleTime} from "rxjs/operators";
import {DEBOUNCE_TIME} from "../shared/_services/download.service";
import {download} from "../shared/_models/download";
import {Saver, SAVER} from "../_providers/saver.provider";
import {ClientDeviceBreakdown} from "../statistics/_models/client-device-breakdown";
import {ActivityGraphData} from "../statistics/_components/activity-graph/activity-graph.component";
import {ReadingPace, ReadingPaceType} from "../statistics/_components/reading-pace/reading-pace.component";
import {Breakdown} from "../statistics/_models/breakdown";
import {SpreadStats} from "../statistics/_models/stats/spread-stats";
import {FavoriteAuthor} from "../statistics/_models/favorite-author";
import {StatsFilter} from "../statistics/_models/stats-filter";
import {ProfileStatBar} from "../profile/_components/profile-stat-bar/profile-stat-bar.component";
import {
  ReadTimeByHour
} from "../statistics/_components/avg-time-spend-reading-by-hour/avg-time-spend-reading-by-hour.component";
import {StatBucket} from "../statistics/_models/stats/stat-bucket";
import {Genre} from "../_models/metadata/genre";
import {Library} from "../_models/library/library";
import {Series} from "../_models/series";
import {Tag} from "../_models/tag";
import {Person, PersonRole} from "../_models/metadata/person";
import {ReadingList} from "../_models/reading-list";

export enum DayOfWeek
{
    Sunday = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
}

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {
  private httpClient = inject(HttpClient);
  private save = inject<Saver>(SAVER);


  baseUrl = environment.apiUrl;
  translocoService = inject(TranslocoService);
  publicationStatusPipe = new PublicationStatusPipe();


  getUserStatisticsResource(userId: () => number) {
    return httpResource<UserReadStatistics>(() => this.baseUrl + `stats/user-read?userId=${userId()}`).asReadonly();
  }

  getServerStatisticsResource() {
    return httpResource<ServerStatistics>(() => this.baseUrl + 'stats/server/stats').asReadonly();
  }

  getPopularLibraries() {
    return httpResource<StatCount<Library>[]>(() => this.baseUrl + 'stats/popular-libraries').asReadonly();
  }

  getPopularSeries() {
    return httpResource<StatCount<Series>[]>(() => this.baseUrl + 'stats/popular-series').asReadonly();
  }

  getPopularReadingList() {
    return httpResource<StatCount<ReadingList>[]>(() => this.baseUrl + 'stats/popular-reading-list').asReadonly();
  }

  getPopularGenresResource() {
    return httpResource<StatCount<Genre>[]>(() => this.baseUrl + 'stats/popular-genres').asReadonly();
  }

  getPopularTagsResource() {
    return httpResource<StatCount<Tag>[]>(() => this.baseUrl + 'stats/popular-tags').asReadonly();
  }

  getPopularPersonResource(role: PersonRole) {
    return httpResource<StatCount<Person>[]>(() => this.baseUrl + `stats/popular-people?role=${role}`).asReadonly();
  }

  getPopularDecadesResource() {
    return httpResource<StatBucket[]>(() => this.baseUrl + 'stats/popular-decades').asReadonly();
  }

  getFilesAddedOverTime() {
    return httpResource<StatCountWithFormat<string>[]>(() => this.baseUrl + 'stats/files-added-over-time').asReadonly();
  }

  getPagesPerYear(userId = 0) {
    return this.httpClient.get<StatCount<number>[]>(this.baseUrl + 'stats/pages-per-year?userId=' + userId).pipe(
      map(spreads => spreads.map(spread => {
        return {name: spread.value + '', value: spread.count};
      })));
  }

  getWordsPerYear(userId = 0) {
    return this.httpClient.get<StatCount<number>[]>(this.baseUrl + 'stats/words-per-year?userId=' + userId).pipe(
      map(spreads => spreads.map(spread => {
        return {name: spread.value + '', value: spread.count};
      })));
  }


  getMostActiveUsers(statsFilter: () => StatsFilter | undefined) {
    return this.filterServerResource<MostActiveUser[]>(statsFilter, 'most-active-users');
  }


  getPublicationStatus() {
    return this.httpClient.get<StatCount<PublicationStatus>[]>(this.baseUrl + 'stats/server/count/publication-status').pipe(
      map(spreads => spreads.map(spread => {
      return {name: this.publicationStatusPipe.transform(spread.value), value: spread.count};
    })));
  }

  getMangaFormat() {
    return this.httpClient.get<StatCount<MangaFormat>[]>(this.baseUrl + 'stats/server/count/manga-format');
  }

  getClientDeviceBreakdown() {
    return this.httpClient.get<ClientDeviceBreakdown>(this.baseUrl + 'stats/device/client-type');
  }

  getClientDeviceTypeCounts() {
    return this.httpClient.get<StatCount<string>[]>(this.baseUrl + 'stats/device/device-type');
  }


  getFileBreakdown() {
    return this.httpClient.get<FileExtensionBreakdown>(this.baseUrl + 'stats/server/file-breakdown');
  }

  downloadFileBreakdown(extension: string) {
    return this.httpClient.get(this.baseUrl + 'stats/server/file-extension?fileExtension=' + encodeURIComponent(extension),
      {observe: 'events', responseType: 'blob', reportProgress: true}
    ).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      }),
      // tap((d) => this.updateDownloadState(d, downloadType, subtitle, 0)),
      // finalize(() => this.finalizeDownloadState(downloadType, subtitle))
    );

  }

  getReadCountResource(statsFilter: () => StatsFilter, userId: () => number = () => 0) {
    return httpResource<Array<StatCountWithFormat<any>>>(() => {
      const filter = statsFilter();
      if (!filter) return undefined;

      return {
        url: this.baseUrl + `stats/reading-counts`,
        params: this.filterHttpParams(filter, userId())
      }
    });
  }

  getDayBreakdown(userId = 0) {
    return this.httpClient.get<Array<StatCount<DayOfWeek>>>(this.baseUrl + 'stats/day-breakdown?userId=' + userId);
  }

  getReadingActivityResource(statsFilter: () => (StatsFilter | undefined), userId: () => number, year: () => number) {
    return httpResource<ActivityGraphData>(() => {
      const filter = statsFilter();
      if (!filter) return undefined;

      return {
        url: this.baseUrl + `stats/reading-activity?year=${year()}`,
        params: this.filterHttpParams(filter, userId())
      }
    }).asReadonly();
  }

  getReadingPaceResource(statsFilter: () => (StatsFilter | undefined), userId: () => number, year: () => number, type: () => ReadingPaceType) {
    return httpResource<ReadingPace>(() => {
      const filter = statsFilter();
      if (!filter) return undefined;

      let params = this.filterHttpParams(filter, userId());
      if (type() === ReadingPaceType.Books) {
        params = params.append('booksOnly', true)
      }

      return {
        url: this.baseUrl + `stats/reading-pace?year=${year()}`,
        params: params
      }
    }).asReadonly();
  }

  getPreferredFormatResource(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<StatCount<MangaFormat>[]>(statsFilter, userId, 'preferred-format')
  }

  private filterHttpParams(filter: StatsFilter, userId: number | undefined = undefined) {
    let params = new HttpParams();

    if (userId !== undefined) {
      params = params.set('userId', userId)
    }

    if (filter.timeFilter.startDate) {
      params = params.set('startDate', filter.timeFilter.startDate.toISOString());
    }
    if (filter.timeFilter.endDate) {
      params = params.set('endDate', filter.timeFilter.endDate.toISOString());
    }

    for (let library of filter.libraries) {
      params = params.append('libraries', library)
    }

    return params;
  }


  private filterResource<T>(
    statsFilter: () => (StatsFilter | undefined),
    userId: () => number,
    path: string
  ) {
    return httpResource<T>(() => {
      const filter = statsFilter();
      if (!filter) return undefined; // skip request until valid

      return {
        url: `${this.baseUrl}stats/${path}`,
        params: this.filterHttpParams(filter, userId()),
      };
    }).asReadonly();
  }

  private filterServerResource<T>(
    statsFilter: () => (StatsFilter | undefined),
    path: string
  ) {
    return httpResource<T>(() => {
      const filter = statsFilter();
      if (!filter) return undefined; // skip request until valid

      return {
        url: `${this.baseUrl}stats/${path}`,
        params: this.filterHttpParams(filter),
      };
    }).asReadonly();
  }

  getGenreBreakDownResource(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<Breakdown<string>>(statsFilter, userId, 'genre-breakdown');
  }

  getTagBreakDownResource(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<Breakdown<string>>(statsFilter, userId, 'tag-breakdown');
  }

  getPageSpread(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<SpreadStats>(statsFilter, userId, 'page-spread');
  }

  getWordSpread(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<SpreadStats>(statsFilter, userId, 'word-spread');
  }

  getFavouriteAuthors(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<FavoriteAuthor[]>(statsFilter, userId, 'favorite-authors');
  }

  getReadsByMonths(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<StatCount<{year: number, month: number}>[]>(statsFilter, userId, 'reads-by-month');
  }

  getAvgTimeSpendReadingByHour(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<ReadTimeByHour>(statsFilter, userId, 'avg-time-by-hour');
  }

  getUserOverallStats(statsFilter: () => StatsFilter | undefined, userId: () => number) {
    return this.filterResource<ProfileStatBar>(statsFilter, userId, 'user-stats');
  }

  getTotalReads(userId: () => number) {
    return httpResource<number>(() => this.baseUrl + `stats/total-reads?userId=${userId()}`).asReadonly();
  }

}
