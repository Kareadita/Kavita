import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from 'src/environments/environment';
import { UtilityService } from '../shared/_services/utility.service';
import { PaginatedResult } from '../_models/pagination';
import { Series } from '../_models/series';

@Injectable({
  providedIn: 'root'
})
export class RecommendationService {

  private baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  getQuickReads(libraryId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/quick-reads?libraryId=' + libraryId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response)));
  }

  getQuickCatchupReads(libraryId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/quick-catchup-reads?libraryId=' + libraryId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response)));
  }

  getHighlyRated(libraryId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/highly-rated?libraryId=' + libraryId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response)));
  }

  getRediscover(libraryId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/rediscover?libraryId=' + libraryId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response)));
  }

  getMoreIn(libraryId: number, genreId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/more-in?libraryId=' + libraryId + '&genreId=' + genreId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response)));
  }
}
