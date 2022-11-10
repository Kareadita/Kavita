import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { distinctUntilChanged, filter, map, Observable, of, ReplaySubject, startWith, switchMap } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SearchResultGroup } from '../_models/search/search-result-group';
import { Series } from '../_models/series';

@Injectable({
  providedIn: 'root'
})
export class SearchService {

  baseUrl = environment.apiUrl;

  private searchSubject: ReplaySubject<string> = new ReplaySubject(1);
  searchResults$: Observable<SearchResultGroup>;
  searchTerm$: Observable<string> = this.searchSubject.asObservable();

  constructor(private httpClient: HttpClient) {
    this.searchResults$ = this.searchSubject.pipe(
      startWith(''),
      map(val => val.trim()),
      distinctUntilChanged(),
      switchMap(term => {
        if (term === '' || term === null || term === undefined) return of(new SearchResultGroup());
        return this.httpClient.get<SearchResultGroup>(this.baseUrl + 'search/search?queryString=' + encodeURIComponent(term))
      })
    );
  }

  search(term: string) {
    this.searchSubject.next(term);
  }

  getSeriesForMangaFile(mangaFileId: number) {
    return this.httpClient.get<Series | null>(this.baseUrl + 'search/series-for-mangafile?mangaFileId=' + mangaFileId);
  }

  getSeriesForChapter(chapterId: number) {
    return this.httpClient.get<Series | null>(this.baseUrl + 'search/series-for-chapter?chapterId=' + chapterId);
  }
}
