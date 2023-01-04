import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { UserReadStatistics } from '../statistics/_models/user-read-statistics';
import { PublicationStatusPipe } from '../pipe/publication-status.pipe';
import { map } from 'rxjs';
import { MangaFormatPipe } from '../pipe/manga-format.pipe';
import { FileExtensionBreakdown } from '../statistics/_models/file-breakdown';
import { TopUserRead } from '../statistics/_models/top-reads';
import { ReadHistoryEvent } from '../statistics/_models/read-history-event';
import { ServerStatistics } from '../statistics/_models/server-statistics';
import { StatCount } from '../statistics/_models/stat-count';
import { PublicationStatus } from '../_models/metadata/publication-status';
import { MangaFormat } from '../_models/manga-format';

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

const publicationStatusPipe = new PublicationStatusPipe();
const mangaFormatPipe = new MangaFormatPipe();

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

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

  getTopUsers(days: number = 0) {
    return this.httpClient.get<TopUserRead[]>(this.baseUrl + 'stats/server/top/users?days=' + days);
  }

  getReadingHistory(userId: number) {
    return this.httpClient.get<ReadHistoryEvent[]>(this.baseUrl + 'stats/user/reading-history?userId=' + userId);
  }

  getPublicationStatus() {
    return this.httpClient.get<StatCount<PublicationStatus>[]>(this.baseUrl + 'stats/server/count/publication-status').pipe(
      map(spreads => spreads.map(spread => {
      return {name: publicationStatusPipe.transform(spread.value), value: spread.count};
      })));
  }

  getMangaFormat() {
    return this.httpClient.get<StatCount<MangaFormat>[]>(this.baseUrl + 'stats/server/count/manga-format').pipe(
      map(spreads => spreads.map(spread => {
      return {name: mangaFormatPipe.transform(spread.value), value: spread.count};
      })));
  }

  getTotalSize() {
    return this.httpClient.get<number>(this.baseUrl + 'stats/server/file-size', { responseType: 'text' as 'json'});
  }

  getFileBreakdown() {
    return this.httpClient.get<FileExtensionBreakdown>(this.baseUrl + 'stats/server/file-breakdown');
  }

  getReadCountByDay(userId: number = 0, days: number = 0) {
    return this.httpClient.get<Array<any>>(this.baseUrl + 'stats/reading-count-by-day?userId=' + userId + '&days=' + days);
  }

  getDayBreakdown() {
    return this.httpClient.get<Array<StatCount<DayOfWeek>>>(this.baseUrl + 'stats/day-breakdown');
  }
}
