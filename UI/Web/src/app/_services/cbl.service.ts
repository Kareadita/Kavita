import {inject, Injectable} from '@angular/core';
import {HttpClient, HttpParams} from '@angular/common/http';
import {environment} from '../../environments/environment';
import {CblRepoBrowseResult} from '../_models/reading-list/cbl/cbl-repo-browse-result';
import {CblRepoItem} from '../_models/reading-list/cbl/cbl-repo-item';
import {CblImportSummary} from '../_models/reading-list/cbl/cbl-import-summary';
import {NgxFileDropEntry} from 'ngx-file-drop';

@Injectable({
  providedIn: 'root',
})
export class CblService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  browseRepo(path: string = '') {
    let params = new HttpParams();
    if (path !== '') {
      params = params.append('path', path);
    }
    return this.httpClient.get<CblRepoBrowseResult>(this.baseUrl + 'cbl/browse', {params: params});
  }

  importFromRepo(items: CblRepoItem[]) {
    return this.httpClient.post(this.baseUrl + 'cbl/repo-import', {items});
  }

  importFromUrl(url: string) {
    return this.httpClient.post<CblImportSummary>(this.baseUrl + 'cbl/upload-cbl-file', {url});
  }

  importFromFile(file: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData();
    formData.append('cblFile', file, fileEntry.relativePath);
    return this.httpClient.post<CblImportSummary>(this.baseUrl + 'cbl/file-import', formData);
  }
}
