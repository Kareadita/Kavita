import {Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TRANSLOCO_LOADER, TranslocoLoader} from "@ngneat/transloco";

@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  constructor(private http: HttpClient) {}

  getTranslation(lang: string) {
    return this.http.get<Translation>(`/assets/langs/${lang}.json`);
  }
}

export const httpLoader = { provide: TRANSLOCO_LOADER, useClass: HttpLoader };
