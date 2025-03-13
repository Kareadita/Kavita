import {inject, Injectable} from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {KavitaLocale, Language} from "../_models/metadata/language";
import {ReplaySubject, tap} from "rxjs";
import {TranslocoService} from "@jsverse/transloco";

@Injectable({
  providedIn: 'root'
})
export class LocalizationService {

  private readonly translocoService = inject(TranslocoService);

  baseUrl = environment.apiUrl;

  private readonly localeSubject = new ReplaySubject<KavitaLocale[]>(1);
  public readonly locales$ = this.localeSubject.asObservable();

  constructor(private httpClient: HttpClient) { }

  getLocales() {
    return this.httpClient.get<KavitaLocale[]>(this.baseUrl + 'locale').pipe(tap(locales => {
      this.localeSubject.next(locales);
    }));
  }

  refreshTranslations(lang: string) {

    // Clear the cached translation
    localStorage.removeItem(`@@TRANSLOCO_PERSIST_TRANSLATIONS/${lang}`);

    // Reload the translation
    return this.translocoService.load(lang);
  }
}
