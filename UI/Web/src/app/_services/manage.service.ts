import {inject, Injectable} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {ManageMatchSeries} from "../_models/kavitaplus/manage-match-series";
import {ManageMatchFilter} from "../_models/kavitaplus/manage-match-filter";

@Injectable({
  providedIn: 'root'
})
export class ManageService {

  baseUrl = environment.apiUrl;
  private readonly httpClient = inject(HttpClient);

  getAllKavitaPlusSeries(filter: ManageMatchFilter) {
    return this.httpClient.post<Array<ManageMatchSeries>>(this.baseUrl + `manage/series-metadata`, filter);
  }
}
