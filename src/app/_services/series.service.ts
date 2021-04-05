import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Chapter } from '../_models/chapter';
import { InProgressChapter } from '../_models/in-progress-chapter';
import { PaginatedResult } from '../_models/pagination';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;
  paginatedResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();

  constructor(private httpClient: HttpClient) { }

  getSeriesForLibrary(libraryId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();

    if (pageNum !== null && itemsPerPage !== null) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'series?libraryId=' + libraryId, {observe: 'response', params}).pipe(
      map((response: any) => {
        if (response.body === null) {
          this.paginatedResults.result = [];
        } else {
          this.paginatedResults.result = response.body;
        }

        const pageHeader = response.headers.get('Pagination');
        if (pageHeader !== null) {
          this.paginatedResults.pagination = JSON.parse(pageHeader);
        }

        return this.paginatedResults;
      })
    );
  }

  getSeries(seriesId: number) {
    return this.httpClient.get<Series>(this.baseUrl + 'series/' + seriesId);
  }

  getVolumes(seriesId: number) {
    return this.httpClient.get<Volume[]>(this.baseUrl + 'series/volumes?seriesId=' + seriesId);
  }

  getVolume(volumeId: number) {
    return this.httpClient.get<Volume>(this.baseUrl + 'series/volume?volumeId=' + volumeId);
  }

  getChapter(chapterId: number) {
    return this.httpClient.get<Chapter>(this.baseUrl + 'series/chapter?chapterId=' + chapterId);
  }

  getData(id: number) {
    return of(id);
  }

  delete(seriesId: number) {
    return this.httpClient.delete<boolean>(this.baseUrl + 'series/' + seriesId);
  }

  updateRating(seriesId: number, userRating: number, userReview: string) {
    return this.httpClient.post(this.baseUrl + 'series/update-rating', {seriesId, userRating, userReview});
  }

  updateSeries(model: any) {
    return this.httpClient.post(this.baseUrl + 'series/', model);
  }

  markRead(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-read', {seriesId});
  }

  markUnread(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-unread', {seriesId});
  }

  getRecentlyAdded(libraryId: number = 0) {
    return this.httpClient.get<Series[]>(this.baseUrl + 'series/recently-added?libraryId=' + libraryId);
  }

  getInProgress(libraryId: number = 0) {
    return this.httpClient.get<Series[]>(this.baseUrl + 'series/in-progress?libraryId=' + libraryId);
  }

  getContinueReading(libraryId: number = 0) {
    return this.httpClient.get<InProgressChapter[]>(this.baseUrl + 'series/continue-reading?libraryId=' + libraryId);
  }
}
