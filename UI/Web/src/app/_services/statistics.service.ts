import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { YearCount } from '../statistics/_models/year-count';
import { UserReadStatistics } from '../statistics/_models/user-read-statistics';
import { PublicationCount } from '../statistics/_models/publication-count';
import { MangaFormatCount } from '../statistics/_models/manga-format-count';
import { PublicationStatusPipe } from '../pipe/publication-status.pipe';
import { map } from 'rxjs';
import { MangaFormatPipe } from '../pipe/manga-format.pipe';


const publicationStatusPipe = new PublicationStatusPipe();
const mangaFormatPipe = new MangaFormatPipe();

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getUserStatistics(userId: number, libraryIds: Array<number> = []) {
    let url = 'stats/user/' + userId + '/read';
    if (libraryIds.length > 0) url += '?libraryIds=' + libraryIds.join(',');
    
    return this.httpClient.get<UserReadStatistics>(this.baseUrl + url);
  }

  getYearRange() {
    return this.httpClient.get<YearCount[]>(this.baseUrl + 'stats/server/count/year').pipe(
      map(spreads => spreads.map(spread => {
      return {name: spread.value + '', value: spread.count};
      })));
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
}
