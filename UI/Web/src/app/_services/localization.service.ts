import {inject, Injectable} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {KavitaLocale} from "../_models/metadata/language";
import {ReplaySubject, tap} from "rxjs";
import {TranslocoService} from "@jsverse/transloco";

@Injectable({
  providedIn: 'root'
})
export class LocalizationService {
  private httpClient = inject(HttpClient);


  private readonly translocoService = inject(TranslocoService);

  baseUrl = environment.apiUrl;

  private readonly localeSubject = new ReplaySubject<KavitaLocale[]>(1);
  public readonly locales$ = this.localeSubject.asObservable();

  getLocales() {
    return this.httpClient.get<KavitaLocale[]>(this.baseUrl + 'locale').pipe(tap(locales => {
      this.localeSubject.next(locales);
    }));
  }

  refreshTranslations(newLang: string) {

    // Clear the cached translation
    localStorage.removeItem(`@transloco/translations`);
    localStorage.removeItem(`@transloco/translations/timestamp`);

    // Reload the translation
    this.translocoService.setActiveLang(newLang);
    return this.translocoService.load(newLang);
  }
}
