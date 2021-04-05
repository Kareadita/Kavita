import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Library } from '../_models/library';
import { SearchResult } from '../_models/search-result';

@Injectable({
  providedIn: 'root'
})
export class LibraryService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  listDirectories(rootPath: string) {
    let query = '';
    if (rootPath !== undefined && rootPath.length > 0) {
      query = '?path=' + rootPath;
    }

    return this.httpClient.get<string[]>(this.baseUrl + 'library/list' + query);
  }

  getLibraries() {
    return this.httpClient.get<Library[]>(this.baseUrl + 'library');
  }

  getLibrariesForMember() {
    return this.httpClient.get<Library[]>(this.baseUrl + 'library/libraries');
  }

  updateLibrariesForMember(username: string, selectedLibraries: Library[]) {
    return this.httpClient.post(this.baseUrl + 'library/grant-access', {username, selectedLibraries});
  }

  scan(libraryId: number) {
    return this.httpClient.post(this.baseUrl + 'library/scan?libraryId=' + libraryId, {});
  }

  refreshMetadata(libraryId: number) {
    return this.httpClient.post(this.baseUrl + 'library/refresh-metadata?libraryId=' + libraryId, {});
  }

  create(model: {name: string, type: number, folders: string[]}) {
    return this.httpClient.post(this.baseUrl + 'library/create', model);
  }

  delete(libraryId: number) {
    return this.httpClient.delete(this.baseUrl + 'library/delete?libraryId=' + libraryId, {});
  }

  update(model: {name: string, folders: string[], id: number}) {
    return this.httpClient.post(this.baseUrl + 'library/update', model);
  }

  search(term: string) {
    // TODO: MOve to search service
    if (term === '') {
      return of([]);
    }
     // TODO: Url encode this
    return this.httpClient.get<SearchResult[]>(this.baseUrl + 'library/search?queryString=' + term);
  }

}
