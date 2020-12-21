import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { Library } from '../_models/library';

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

  getLibrariesForMember(username: string) {
    return this.httpClient.get<Library[]>(this.baseUrl + 'library/' + username);
  }

  updateLibrariesForMember(username: string, selectedLibraries: Library[]) {
    return this.httpClient.post(this.baseUrl + '/library/update-for', {username, selectedLibraries});
  }


}
