import { Injectable, inject } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {ExternalSource} from "../_models/sidenav/external-source";
import {TextResonse} from "../_types/text-response";
import {map} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class ExternalSourceService {
  private httpClient = inject(HttpClient);


  baseUrl = environment.apiUrl;

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
    const dto = {id: 0, name, host, apiKey};

    return this.httpClient.post<string>(this.baseUrl + `stream/external-source-exists`, dto, TextResonse)
      .pipe(map(s => s == 'true'));
  }
}
