import { Injectable, inject } from '@angular/core';
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";

@Injectable({
  providedIn: 'root'
})
export class FilterService {
  private httpClient = inject(HttpClient);


  baseUrl = environment.apiUrl;

  saveFilter(filter: FilterV2<number>) {
    return this.httpClient.post(this.baseUrl + 'filter/update', filter);
  }
  getAllFilters() {
    return this.httpClient.get<Array<SmartFilter>>(this.baseUrl + 'filter');
  }
  deleteFilter(filterId: number) {
    return this.httpClient.delete(this.baseUrl + 'filter?filterId=' + filterId);
  }

  renameSmartFilter(filter: SmartFilter) {
    return this.httpClient.post(this.baseUrl + `filter/rename?filterId=${filter.id}&name=${filter.name.trim()}`, {});
  }
}
