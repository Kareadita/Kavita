import {Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TranslocoLoader} from "@ngneat/transloco";
import cacheBusting from 'i18n-cache-busting.json'; // allowSyntheticDefaultImports must be true

@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  constructor(private http: HttpClient) {}

  getTranslation(langPath: string) {
    const tokens = langPath.split('/');
    const langCode = tokens[tokens.length - 1];
    return this.http.get<Translation>(`assets/langs/${langCode}.json?v=${(cacheBusting as { [key: string]: string })[langCode]}`);
  }
}
