import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {KavitaLocale, Language} from "../_models/metadata/language";
import {ReplaySubject, tap} from "rxjs";

@Injectable({
  providedIn: 'root'
})
export class LocalizationService {

  baseUrl = environment.apiUrl;

  private readonly localeSubject = new ReplaySubject<KavitaLocale[]>(1);
  public readonly locales$ = this.localeSubject.asObservable();

  constructor(private httpClient: HttpClient) { }

  getLocales() {
    return this.httpClient.get<KavitaLocale[]>(this.baseUrl + 'locale/l2').pipe(tap(locales => {
      this.localeSubject.next(locales);
    }));
  }
}
