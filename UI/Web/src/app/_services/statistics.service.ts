import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { YearCount } from '../statistics/_models/year-count';
import { UserReadStatistics } from '../statistics/_models/user-read-statistics';
import { PublicationCount } from '../statistics/_models/publication-count';
import { MangaFormatCount } from '../statistics/_models/manga-format-count';
import { PublicationStatusPipe } from '../pipe/publication-status.pipe';
import { map } from 'rxjs';
import { MangaFormatPipe } from '../pipe/manga-format.pipe';
import { FileExtensionBreakdown } from '../statistics/_models/file-breakdown';
import { TopReads } from '../statistics/_models/top-reads';
import { ReadHistoryEvent } from '../statistics/_models/read-history-event';
import { ServerStatistics } from '../statistics/_models/server-statistics';


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
    return this.httpClient.get<YearCount[]>(this.baseUrl + 'stats/server/count/year').pipe(
      map(spreads => spreads.map(spread => {
      return {name: spread.value + '', value: spread.count};
      })));
  }

  getTopYears() {
    return this.httpClient.get<YearCount[]>(this.baseUrl + 'stats/server/top/years').pipe(
      map(spreads => spreads.map(spread => {
      return {name: spread.value + '', value: spread.count};
      })));
  }

  getTopReads(username: string = 'All users', days: number = 0) {
    const params = new HttpParams();
    if (username !== 'All users' || days !== 0)  {
      if (username !== 'All users') params.append('username', encodeURIComponent(username));
      if (days !== 0) params.append('days', days);
    }
    
    return this.httpClient.get<TopReads>(this.baseUrl + 'stats/server/top/reads', {params: params});
  }

  getReadingHistory(userId: number) {
    return this.httpClient.get<ReadHistoryEvent[]>(this.baseUrl + 'stats/user/reading-history?userId=' + userId);
  }

  getPublicationStatus() {
    return this.httpClient.get<PublicationCount[]>(this.baseUrl + 'stats/server/count/publication-status').pipe(
      map(spreads => spreads.map(spread => {
      return {name: publicationStatusPipe.transform(spread.value), value: spread.count};
      })));
  }

  getMangaFormat() {
    return this.httpClient.get<MangaFormatCount[]>(this.baseUrl + 'stats/server/count/manga-format').pipe(
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
}
