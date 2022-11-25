import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { YearSpread } from '../statistics/_models/year-spread';
import { UserReadStatistics } from '../statistics/_models/user-read-statistics';

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
    return this.httpClient.get<YearSpread[]>(this.baseUrl + 'stats/server/year');
  }
}
