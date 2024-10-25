import { Injectable } from '@angular/core';
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {JumpKey} from "../_models/jumpbar/jump-key";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";

@Injectable({
  providedIn: 'root'
})
export class FilterService {

  baseUrl = environment.apiUrl;
  constructor(private httpClient: HttpClient) { }

  saveFilter(filter: SeriesFilterV2) {
    return this.httpClient.post(this.baseUrl + 'filter/update', filter);
  }
  getAllFilters() {
    return this.httpClient.get<Array<SmartFilter>>(this.baseUrl + 'filter');
  }
  deleteFilter(filterId: number) {
    return this.httpClient.delete(this.baseUrl + 'filter?filterId=' + filterId);
  }

}
