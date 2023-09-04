import { Injectable } from '@angular/core';
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {JumpKey} from "../_models/jumpbar/jump-key";

@Injectable({
  providedIn: 'root'
})
export class FilterService {

  baseUrl = environment.apiUrl;
  constructor(private httpClient: HttpClient) { }

  saveFilter(filter: SeriesFilterV2) {
    return this.httpClient.post(this.baseUrl + 'filter/update', filter);
  }

}
