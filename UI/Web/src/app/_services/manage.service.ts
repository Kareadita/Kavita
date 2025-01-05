import {inject, Injectable} from '@angular/core';
import {environment} from "../../environments/environment";
import {LicenseInfo} from "../_models/kavitaplus/license-info";
import {HttpClient} from "@angular/common/http";
import {ManageMatchSeries} from "../_models/kavitaplus/manage-match-series";

@Injectable({
  providedIn: 'root'
})
export class ManageService {

  baseUrl = environment.apiUrl;
  private readonly httpClient = inject(HttpClient);

  getAllKavitaPlusSeries() {
    return this.httpClient.get<Array<ManageMatchSeries>>(this.baseUrl + `manage/series-metadata`)
  }
}
