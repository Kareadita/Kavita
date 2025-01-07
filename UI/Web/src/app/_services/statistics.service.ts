import { HttpClient } from '@angular/common/http';
import {Inject, inject, Injectable} from '@angular/core';
import { environment } from 'src/environments/environment';
import { UserReadStatistics } from '../statistics/_models/user-read-statistics';
import { PublicationStatusPipe } from '../_pipes/publication-status.pipe';
import {asyncScheduler, finalize, map, tap} from 'rxjs';
import { MangaFormatPipe } from '../_pipes/manga-format.pipe';
import { FileExtensionBreakdown } from '../statistics/_models/file-breakdown';
import { TopUserRead } from '../statistics/_models/top-reads';
import { ReadHistoryEvent } from '../statistics/_models/read-history-event';
import { ServerStatistics } from '../statistics/_models/server-statistics';
import { StatCount } from '../statistics/_models/stat-count';
import { PublicationStatus } from '../_models/metadata/publication-status';
import { MangaFormat } from '../_models/manga-format';
import { TextResonse } from '../_types/text-response';
import {TranslocoService} from "@jsverse/transloco";
import {KavitaPlusMetadataBreakdown} from "../statistics/_models/kavitaplus-metadata-breakdown";
import {throttleTime} from "rxjs/operators";
import {DEBOUNCE_TIME} from "../shared/_services/download.service";
import {download} from "../shared/_models/download";
import {Saver, SAVER} from "../_providers/saver.provider";

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

  baseUrl = environment.apiUrl;
  translocoService = inject(TranslocoService);
  publicationStatusPipe = new PublicationStatusPipe(this.translocoService);
  mangaFormatPipe = new MangaFormatPipe(this.translocoService);

  constructor(private httpClient: HttpClient, @Inject(SAVER) private save: Saver) { }

  getUserStatistics(userId: number, libraryIds: Array<number> = []) {
    // TODO: Convert to httpParams object
    let url = 'stats/user/' + userId + '/read';
    if (libraryIds.length > 0) url += '?libraryIds=' + libraryIds.join(',');

    return this.httpClient.get<UserReadStatistics>(this.baseUrl + url);
  }

  getServerStatistics() {
    return this.httpClient.get<ServerStatistics>(this.baseUrl + 'stats/server/stats');
  }

  getYearRange() {
    return this.httpClient.get<StatCount<number>[]>(this.baseUrl + 'stats/server/count/year').pipe(
      map(spreads => spreads.map(spread => {
      return {name: spread.value + '', value: spread.count};
      })));
  }

  getTopYears() {
    return this.httpClient.get<StatCount<number>[]>(this.baseUrl + 'stats/server/top/years').pipe(
      map(spreads => spreads.map(spread => {
        return {name: spread.value + '', value: spread.count};
      })));
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

  getTopUsers(days: number = 0) {
    return this.httpClient.get<TopUserRead[]>(this.baseUrl + 'stats/server/top/users?days=' + days);
  }

  getReadingHistory(userId: number) {
    return this.httpClient.get<ReadHistoryEvent[]>(this.baseUrl + 'stats/user/reading-history?userId=' + userId);
  }

  getPublicationStatus() {
    return this.httpClient.get<StatCount<PublicationStatus>[]>(this.baseUrl + 'stats/server/count/publication-status').pipe(
      map(spreads => spreads.map(spread => {
      return {name: this.publicationStatusPipe.transform(spread.value), value: spread.count};
      })));
  }

  getMangaFormat() {
    return this.httpClient.get<StatCount<MangaFormat>[]>(this.baseUrl + 'stats/server/count/manga-format').pipe(
      map(spreads => spreads.map(spread => {
      return {name: this.mangaFormatPipe.transform(spread.value), value: spread.count};
      })));
  }

  getTotalSize() {
    return this.httpClient.get<number>(this.baseUrl + 'stats/server/file-size', TextResonse);
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

  getReadCountByDay(userId: number = 0, days: number = 0) {
    return this.httpClient.get<Array<any>>(this.baseUrl + 'stats/reading-count-by-day?userId=' + userId + '&days=' + days);
  }

  getDayBreakdown( userId = 0) {
    return this.httpClient.get<Array<StatCount<DayOfWeek>>>(this.baseUrl + 'stats/day-breakdown?userId=' + userId);
  }
}
