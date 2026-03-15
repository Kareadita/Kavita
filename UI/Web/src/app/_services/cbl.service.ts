import {inject, Injectable} from '@angular/core';
import {HttpClient, HttpParams} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {CblRepoBrowseResult} from "../_models/reading-list/cbl/cbl-repo-browse-result";

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
}
