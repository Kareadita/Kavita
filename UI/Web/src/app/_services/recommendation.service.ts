import {HttpClient, HttpParams} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {map, Observable} from 'rxjs';
import {environment} from 'src/environments/environment';
import {UtilityService} from '../shared/_services/utility.service';
import {PaginatedResult} from '../_models/pagination';
import {Series} from '../_models/series';

@Injectable({
  providedIn: 'root'
})
export class RecommendationService {
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);

  private readonly baseUrl = environment.apiUrl;

  getMoreIn(libraryId: number, genreId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'recommended/more-in?libraryId=' + libraryId + '&genreId=' + genreId, {observe: 'response', params})
      .pipe(map(response => this.utilityService.createPaginatedResult(response))) as Observable<PaginatedResult<Series[]>>;
  }
}
