import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SearchResultGroup } from '../_models/search/search-result-group';
import { Series } from '../_models/series';

@Injectable({
  providedIn: 'root'
})
export class SearchService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  search(term: string, includeChapterAndFiles: boolean = false) {
    if (term === '') {
      return of(new SearchResultGroup());
    }
    return this.httpClient.get<SearchResultGroup>(this.baseUrl + `search/search?includeChapterAndFiles=${includeChapterAndFiles}&queryString=${encodeURIComponent(term)}`);
  }

  getSeriesForMangaFile(mangaFileId: number) {
    return this.httpClient.get<Series | null>(this.baseUrl + 'search/series-for-mangafile?mangaFileId=' + mangaFileId);
  }

  getSeriesForChapter(chapterId: number) {
    return this.httpClient.get<Series | null>(this.baseUrl + 'search/series-for-chapter?chapterId=' + chapterId);
  }
}
