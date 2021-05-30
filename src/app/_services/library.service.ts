import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Library, LibraryType } from '../_models/library';
import { SearchResult } from '../_models/search-result';


@Injectable({
  providedIn: 'root'
})
export class LibraryService {

  baseUrl = environment.apiUrl;

  libraryNames: {[key:number]: string} | undefined = undefined;

  constructor(private httpClient: HttpClient) {}

  getLibraryNames() {
    if (this.libraryNames != undefined) {
      return of(this.libraryNames);
    }
    return this.httpClient.get<Library[]>(this.baseUrl + 'library').pipe(map(l => {
      this.libraryNames = {};
      l.forEach(lib => {
        if (this.libraryNames !== undefined) {
          this.libraryNames[lib.id] = lib.name;
        }        
      });
      return this.libraryNames;
    }));
  }

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

  getLibraryType(libraryId: number) {
    // TODO: Cache this in browser
    return this.httpClient.get<LibraryType>(this.baseUrl + 'library/type?libraryId=' + libraryId);
  }

  search(term: string) {
    if (term === '') {
      return of([]);
    }
    return this.httpClient.get<SearchResult[]>(this.baseUrl + 'library/search?queryString=' + encodeURIComponent(term));
  }

}
