import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { InProgressChapter } from '../_models/in-progress-chapter';
import { PaginatedResult } from '../_models/pagination';
import { Series } from '../_models/series';
import { SeriesFilter } from '../_models/series-filter';
import { SeriesMetadata } from '../_models/series-metadata';
import { Volume } from '../_models/volume';
import { ImageService } from './image.service';

@Injectable({
  providedIn: 'root'
})
export class SeriesService {

  baseUrl = environment.apiUrl;
  paginatedResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();
  paginatedSeriesForTagsResults: PaginatedResult<Series[]> = new PaginatedResult<Series[]>();

  constructor(private httpClient: HttpClient, private imageService: ImageService) { }

  _cachePaginatedResults(response: any, paginatedVariable: PaginatedResult<any[]>) {
    if (response.body === null) {
      paginatedVariable.result = [];
    } else {
      paginatedVariable.result = response.body;
    }

    const pageHeader = response.headers.get('Pagination');
    if (pageHeader !== null) {
      paginatedVariable.pagination = JSON.parse(pageHeader);
    }

    return paginatedVariable;
  }

  getSeriesForLibrary(libraryId: number, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);
    const data = this.createSeriesFilter(filter);

    return this.httpClient.post<PaginatedResult<Series[]>>(this.baseUrl + 'series?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, this.paginatedResults);
      })
    );
  }

  getAllSeriesByIds(seriesIds: Array<number>) {
    return this.httpClient.post<Series[]>(this.baseUrl + 'series/series-by-ids', {seriesIds: seriesIds});
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

  deleteMultipleSeries(seriesIds: Array<number>) {
    return this.httpClient.post<boolean>(this.baseUrl + 'series/delete-multiple', {seriesIds});
  }

  updateRating(seriesId: number, userRating: number, userReview: string) {
    return this.httpClient.post(this.baseUrl + 'series/update-rating', {seriesId, userRating, userReview});
  }

  updateSeries(model: any) {
    return this.httpClient.post(this.baseUrl + 'series/update', model);
  }

  markRead(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-read', {seriesId});
  }

  markUnread(seriesId: number) {
    return this.httpClient.post<void>(this.baseUrl + 'reader/mark-unread', {seriesId});
  }

  getRecentlyAdded(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.createSeriesFilter(filter);
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/recently-added?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this._cachePaginatedResults(response, new PaginatedResult<Series[]>());
      })
    );
  }

  getOnDeck(libraryId: number = 0, pageNum?: number, itemsPerPage?: number, filter?: SeriesFilter) {
    const data = this.createSeriesFilter(filter);

    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<Series[]>(this.baseUrl + 'series/on-deck?libraryId=' + libraryId, data, {observe: 'response', params}).pipe(
      map(response => {
        return this._cachePaginatedResults(response, new PaginatedResult<Series[]>());
    }));
  }

  getContinueReading(libraryId: number = 0) {
    return this.httpClient.get<InProgressChapter[]>(this.baseUrl + 'series/continue-reading?libraryId=' + libraryId);
  }

  refreshMetadata(series: Series) {
    return this.httpClient.post(this.baseUrl + 'series/refresh-metadata', {libraryId: series.libraryId, seriesId: series.id});
  }

  scan(libraryId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'series/scan', {libraryId: libraryId, seriesId: seriesId});
  }

  getMetadata(seriesId: number) {
    return this.httpClient.get<SeriesMetadata>(this.baseUrl + 'series/metadata?seriesId=' + seriesId).pipe(map(items => {
      items?.tags.forEach(tag => tag.coverImage = this.imageService.getCollectionCoverImage(tag.id));
      return items;
    }));
  }

  updateMetadata(seriesMetadata: SeriesMetadata, tags: CollectionTag[]) {
    const data = {
      seriesMetadata,
      tags
    };
    return this.httpClient.post(this.baseUrl + 'series/metadata', data, {responseType: 'text' as 'json'});
  }

  getSeriesForTag(collectionTagId: number, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();

    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);
    
    // NOTE: I'm not sure the paginated result is doing anything
    // if (this.paginatedSeriesForTagsResults?.pagination !== undefined && this.paginatedSeriesForTagsResults?.pagination?.currentPage === pageNum) {
    //   return of(this.paginatedSeriesForTagsResults);
    // }

    return this.httpClient.get<PaginatedResult<Series[]>>(this.baseUrl + 'series/series-by-collection?collectionId=' + collectionTagId, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, this.paginatedSeriesForTagsResults);
      })
    );
  }

  _addPaginationIfExists(params: HttpParams, pageNum?: number, itemsPerPage?: number) {
    if (pageNum !== null && pageNum !== undefined && itemsPerPage !== null && itemsPerPage !== undefined) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return params;
  }

  createSeriesFilter(filter?: SeriesFilter) {
    const data: SeriesFilter = {
      mangaFormat: null
    };

    if (filter) {
      data.mangaFormat = filter.mangaFormat;
    }

    return data;
  }
}
