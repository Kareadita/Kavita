import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ReadingList } from '../_models/reading-list';

@Injectable({
  providedIn: 'root'
})
export class ReadingListService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getReadingLists(includePromoted: boolean = true) {
    return this.httpClient.get<ReadingList[]>(this.baseUrl + 'readinglist?includePromoted=' + includePromoted);
  }
}
