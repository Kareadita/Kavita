import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {ExternalSource} from "../_models/sidenav/external-source";
import {TextResonse} from "../_types/text-response";
import {map} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class ExternalSourceService {

  baseUrl = environment.apiUrl;
  constructor(private httpClient: HttpClient) { }

  getExternalSources() {
    return this.httpClient.get<Array<ExternalSource>>(this.baseUrl + 'stream/external-sources');
  }

  createSource(source: ExternalSource) {
    return this.httpClient.post<ExternalSource>(this.baseUrl + 'stream/create-external-source', source);
  }

  updateSource(source: ExternalSource) {
    return this.httpClient.post<ExternalSource>(this.baseUrl + 'stream/update-external-source', source);
  }

  deleteSource(externalSourceId: number) {
    return this.httpClient.delete(this.baseUrl + 'stream/delete-external-source?externalSourceId=' + externalSourceId);
  }

  sourceExists(name: string, host: string, apiKey: string) {
    return this.httpClient.get<string>(this.baseUrl + `stream/external-source-exists?host=${encodeURIComponent(host)}&name=${name}&apiKey=${apiKey}`, TextResonse)
      .pipe(map(s => s == 'true'));
  }
}
