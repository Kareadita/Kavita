import {Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TranslocoLoader} from "@ngneat/transloco";
import {tap} from "rxjs/operators";


@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  constructor(private http: HttpClient) {}

  getTranslation(langPath: string) {
    const tokens = langPath.split('/');
    return this.http.get<Translation>(`assets/langs/${tokens[tokens.length - 1]}.json`)
      .pipe(tap(d => console.log('translations: ', d)));
  }
}
