import {Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TRANSLOCO_LOADER, TranslocoLoader} from "@ngneat/transloco";
import {AccountService} from "./app/_services/account.service";
import {of, switchMap} from "rxjs";
import {catchError} from "rxjs/operators";

@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  constructor(private http: HttpClient, private accountService: AccountService) {}

  getTranslation(lang: string) {
    return this.accountService.currentUser$.pipe(
      switchMap(user => {
        // Get the user's selected locale from the currentUser$.
        // If no user or no locale is available, fallback to 'en'.
        const locale = user?.preferences.locale || 'en';

        // Load the translation file based on the user's locale (e.g., 'en.json', 'es.json', etc.).
        return this.http.get<Translation>(`assets/langs/${locale}.json`).pipe(
          catchError(() => of({})), // Return an empty object if the translation file is not found.
        );
      }),
    );
  }
}

export const httpLoader = { provide: TRANSLOCO_LOADER, useClass: HttpLoader };
