import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ReadingList, ReadingListItem } from '../_models/reading-list';

@Injectable({
  providedIn: 'root'
})
export class ReadingListService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getReadingList(readingListId: number) {
    return this.httpClient.get<ReadingList>(this.baseUrl + 'readinglist?readingListId=' + readingListId);
  }

  getReadingLists(includePromoted: boolean = true) {
    return this.httpClient.get<ReadingList[]>(this.baseUrl + 'readinglist/lists?includePromoted=' + includePromoted);
  }

  getListItems(readingListId: number) {
    return this.httpClient.get<ReadingListItem[]>(this.baseUrl + 'readinglist/items?readingListId=' + readingListId);
  }

  createList(title: string) {
    return this.httpClient.post<ReadingList>(this.baseUrl + 'readinglist/create', {title});
  }

  updateBySeries(readingListId: number, seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'readinglist/update-by-series', {readingListId, seriesId}, { responseType: 'text' as 'json' });
  }

  delete(readingListId: number) {
    return this.httpClient.delete(this.baseUrl + 'readinglist?readingListId=' + readingListId, { responseType: 'text' as 'json' });
  }
}
