import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {Language} from "../_models/metadata/language";

@Injectable({
  providedIn: 'root'
})
export class LocalizationService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getLocales() {
    return this.httpClient.get<Language[]>(this.baseUrl + 'locale');
  }
}
