import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { PaginatedResult } from '../_models/pagination';
import { ReadingList, ReadingListItem } from '../_models/reading-list';
import { ActionItem } from './action-factory.service';

@Injectable({
  providedIn: 'root'
})
export class ReadingListService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getReadingList(readingListId: number) {
    return this.httpClient.get<ReadingList>(this.baseUrl + 'readinglist?readingListId=' + readingListId);
  }

  getReadingLists(includePromoted: boolean = true, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this._addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<ReadingList[]>>(this.baseUrl + 'readinglist/lists?includePromoted=' + includePromoted, {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this._cachePaginatedResults(response, new PaginatedResult<ReadingList[]>());
      })
    );
  }

  getListItems(readingListId: number) {
    return this.httpClient.get<ReadingListItem[]>(this.baseUrl + 'readinglist/items?readingListId=' + readingListId);
  }

  createList(title: string) {
    return this.httpClient.post<ReadingList>(this.baseUrl + 'readinglist/create', {title});
  }

  update(model: {readingListId: number, title?: string, summary?: string, promoted: boolean}) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update', model, { responseType: 'text' as 'json' });
  }

  updateByMultiple(readingListId: number, seriesId: number, volumeIds: Array<number>,  chapterIds?: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-multiple', {readingListId, seriesId, volumeIds, chapterIds});
  }

  updateByMultipleSeries(readingListId: number, seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-multiple-series', {readingListId, seriesIds});
  }

  updateBySeries(readingListId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-series', {readingListId, seriesId}, { responseType: 'text' as 'json' });
  }

  updateByVolume(readingListId: number, seriesId: number, volumeId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-volume', {readingListId, seriesId, volumeId}, { responseType: 'text' as 'json' });
  }

  updateByChapter(readingListId: number, seriesId: number, chapterId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-chapter', {readingListId, seriesId, chapterId}, { responseType: 'text' as 'json' });
  }

  delete(readingListId: number) {
    return this.httpClient.delete(this.baseUrl + 'readinglist?readingListId=' + readingListId, { responseType: 'text' as 'json' });
  }

  updatePosition(readingListId: number, readingListItemId: number, fromPosition: number, toPosition: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-position', {readingListId, readingListItemId, fromPosition, toPosition}, { responseType: 'text' as 'json' });
  }

  deleteItem(readingListId: number, readingListItemId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/delete-item', {readingListId, readingListItemId}, { responseType: 'text' as 'json' });
  }

  removeRead(readingListId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'readinglist/remove-read?readingListId=' + readingListId, {}, { responseType: 'text' as 'json' });
  }

  actionListFilter(action: ActionItem<ReadingList>, readingList: ReadingList, isAdmin: boolean) {
    if (readingList?.promoted && !isAdmin) return false;
    return true;
  }

  _addPaginationIfExists(params: HttpParams, pageNum?: number, itemsPerPage?: number) {
    // TODO: Move to utility service
    if (pageNum !== null && pageNum !== undefined && itemsPerPage !== null && itemsPerPage !== undefined) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return params;
  }

  _cachePaginatedResults(response: any, paginatedVariable: PaginatedResult<any[]>) {
    // TODO: Move to utility service
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
}
