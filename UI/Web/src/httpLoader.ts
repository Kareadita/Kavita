import {inject, Injectable} from "@angular/core";
import {HttpClient} from "@angular/common/http";
import {Translation, TranslocoLoader} from "@jsverse/transloco";
import cacheBusting from 'i18n-cache-busting.json'; // allowSyntheticDefaultImports must be true

@Injectable({ providedIn: 'root' })
export class HttpLoader implements TranslocoLoader {
  private http = inject(HttpClient);

  private loadedVersions: { [key: string]: string } = {};

  getTranslation(langPath: string) {
    const tokens = langPath.split('/');
    const langCode = tokens[tokens.length - 1];

    const currentHash = (cacheBusting as { [key: string]: string })[langCode] || 'en';

    // Check if we've loaded this version before
    const cachedVersion = this.loadedVersions[langCode];

    // If the hash has changed, force a new request and clear local storage cache
    if (cachedVersion && cachedVersion !== currentHash) {
      console.log(`Translation hash changed for ${langCode}. Clearing cache.`);
      this.clearTranslocoCache(langCode);
    }

    // Store the version we're loading
    this.loadedVersions[langCode] = currentHash;

    const url = `assets/langs/${langCode}.json?v=${currentHash}`;
    console.log('Loading locale:', url);

    // Add cache control headers to prevent browser caching
    return this.http.get<Translation>(url, {
      headers: {
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        'Pragma': 'no-cache',
        'Expires': '0'
      }
    });
  }

  /**
   * Clears Transloco cache for a specific language
   */
  private clearTranslocoCache(langCode: string): void {
    localStorage.removeItem('translocoLang');
    localStorage.removeItem('@transloco/translations');
    localStorage.removeItem('@transloco/translations/timestamp');
  }
}
