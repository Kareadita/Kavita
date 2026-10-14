import {inject, Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TranslocoLoader} from "@jsverse/transloco";
import cacheBusting from '../i18n-cache-busting.json'; // allowSyntheticDefaultImports must be true

@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  private http = inject(HttpClient);

  getTranslation(langPath: string) {
    const langCode = langPath.split('/').pop()!;
    const hash = (cacheBusting as Record<string, string>)[langCode] ?? '';

    return this.http.get<Translation>(`assets/langs/${langCode}.json?v=${hash}`);
  }
}
