/// <reference types="@angular/localize" />
import {
  APP_INITIALIZER, ApplicationConfig,
  importProvidersFrom,

} from '@angular/core';
import { AppComponent } from './app/app.component';
import { NgCircleProgressModule } from 'ng-circle-progress';
import { ToastrModule } from 'ngx-toastr';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppRoutingModule } from './app/app-routing.module';
import { SAVER, getSaver } from './app/shared/_providers/saver.provider';
import { Title, BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { JwtInterceptor } from './app/_interceptors/jwt.interceptor';
import { ErrorInterceptor } from './app/_interceptors/error.interceptor';
import {HTTP_INTERCEPTORS, withInterceptorsFromDi, provideHttpClient} from '@angular/common/http';
import {
  provideTransloco,
  TranslocoService
} from "@ngneat/transloco";
import {environment} from "./environments/environment";
import {HttpLoader} from "./httpLoader";
import {
  provideTranslocoPersistLang,
} from '@ngneat/transloco-persist-lang';
import {AccountService} from "./app/_services/account.service";
import {switchMap} from "rxjs";
import {provideTranslocoLocale} from "@ngneat/transloco-locale";
import {provideTranslocoPersistTranslations} from "@ngneat/transloco-persist-translations";

const disableAnimations = !('animate' in document.documentElement);

export function preloadUser(userService: AccountService, transloco: TranslocoService) {
  return function() {
    return userService.currentUser$.pipe(switchMap((user) => {
      if (user && user.preferences.locale) {
        transloco.setActiveLang(user.preferences.locale);
        return transloco.load(user.preferences.locale)
      }

      // If no user or locale is available, fallback to the default language ('en')
      const localStorageLocale = localStorage.getItem(AccountService.localeKey) || 'en';
      transloco.setActiveLang(localStorageLocale);
      return transloco.load(localStorageLocale)
    })).subscribe();
  };
}


export const preLoad = {
  provide: APP_INITIALIZER,
  multi: true,
  useFactory: preloadUser,
  deps: [AccountService, TranslocoService]
};

function transformLanguageCodes(arr: Array<string>) {
    const transformedArray: Array<string> = [];

    arr.forEach(code => {
        // Add the original code
        transformedArray.push(code);

        // Check if the code has a hyphen (like uk-UA)
        if (code.includes('-')) {
            // Transform hyphen to underscore and add to the array
            const transformedCode = code.replace('-', '_');
            transformedArray.push(transformedCode);
        }
    });

    return transformedArray;
}

// All Languages Kavita will support: http://www.lingoes.net/en/translator/langcode.htm
const languageCodes = [
  'af', 'af-ZA', 'ar', 'ar-AE', 'ar-BH', 'ar-DZ', 'ar-EG', 'ar-IQ', 'ar-JO', 'ar-KW',
  'ar-LB', 'ar-LY', 'ar-MA', 'ar-OM', 'ar-QA', 'ar-SA', 'ar-SY', 'ar-TN', 'ar-YE',
  'az', 'az-AZ', 'az-AZ', 'be', 'be-BY', 'bg', 'bg-BG', 'bs-BA', 'ca', 'ca-ES', 'cs',
  'cs-CZ', 'cy', 'cy-GB', 'da', 'da-DK', 'de', 'de-AT', 'de-CH', 'de-DE', 'de-LI', 'de-LU',
  'dv', 'dv-MV', 'el', 'el-GR', 'en', 'en-AU', 'en-BZ', 'en-CA', 'en-CB', 'en-GB', 'en-IE',
  'en-JM', 'en-NZ', 'en-PH', 'en-TT', 'en-US', 'en-ZA', 'en-ZW', 'eo', 'es', 'es-AR', 'es-BO',
  'es-CL', 'es-CO', 'es-CR', 'es-DO', 'es-EC', 'es-ES', 'es-ES', 'es-GT', 'es-HN', 'es-MX',
  'es-NI', 'es-PA', 'es-PE', 'es-PR', 'es-PY', 'es-SV', 'es-UY', 'es-VE', 'et', 'et-EE',
  'eu', 'eu-ES', 'fa', 'fa-IR', 'fi', 'fi-FI', 'fo', 'fo-FO', 'fr', 'fr-BE', 'fr-CA',
  'fr-CH', 'fr-FR', 'fr-LU', 'fr-MC', 'gl', 'gl-ES', 'gu', 'gu-IN', 'he', 'he-IL', 'hi',
  'hi-IN', 'hr', 'hr-BA', 'hr-HR', 'hu', 'hu-HU', 'hy', 'hy-AM', 'id', 'id-ID', 'is',
  'is-IS', 'it', 'it-CH', 'it-IT', 'ja', 'ja-JP', 'ka', 'ka-GE', 'kk', 'kk-KZ', 'kn',
  'kn-IN', 'ko', 'ko-KR', 'kok', 'kok-IN', 'ky', 'ky-KG', 'lt', 'lt-LT', 'lv', 'lv-LV',
  'mi', 'mi-NZ', 'mk', 'mk-MK', 'mn', 'mn-MN', 'mr', 'mr-IN', 'ms', 'ms-BN', 'ms-MY',
  'mt', 'mt-MT', 'nb', 'nb-NO', 'nl', 'nl-BE', 'nl-NL', 'nn-NO', 'ns', 'ns-ZA', 'pa',
  'pa-IN', 'pl', 'pl-PL', 'ps', 'ps-AR', 'pt', 'pt-BR', 'pt-PT', 'qu', 'qu-BO', 'qu-EC',
  'qu-PE', 'ro', 'ro-RO', 'ru', 'ru-RU', 'sa', 'sa-IN', 'se', 'se-FI', 'se-FI', 'se-FI',
  'se-NO', 'se-NO', 'se-NO', 'se-SE', 'se-SE', 'se-SE', 'sk', 'sk-SK', 'sl', 'sl-SI',
  'sq', 'sq-AL', 'sr-BA', 'sr-BA', 'sr-SP', 'sr-SP', 'sv', 'sv-FI', 'sv-SE', 'sw', 'sw-KE',
  'syr', 'syr-SY', 'ta', 'ta-IN', 'te', 'te-IN', 'th', 'th-TH', 'tl', 'tl-PH', 'tn',
  'tn-ZA', 'tr', 'tr-TR', 'tt', 'tt-RU', 'ts', 'uk', 'uk-UA', 'ur', 'ur-PK', 'uz',
  'uz-UZ', 'uz-UZ', 'vi', 'vi-VN', 'xh', 'xh-ZA', 'zh', 'zh-CN', 'zh-HK', 'zh-MO',
  'zh-SG', 'zh-TW', 'zu', 'zu-ZA', 'zh_Hans',
];

const translocoOptions = {
  config: {
    reRenderOnLangChange: true,
    availableLangs: transformLanguageCodes(languageCodes),
    prodMode: environment.production,
    defaultLang: 'en',
    fallbackLang: 'en',
    missingHandler: {
      useFallbackTranslation: true,
      allowEmpty: false,
    }
  }
};

bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule,
          AppRoutingModule,
          BrowserAnimationsModule.withConfig({ disableAnimations }),
          ToastrModule.forRoot({
            positionClass: 'toast-bottom-right',
            preventDuplicates: true,
            timeOut: 6000,
            countDuplicates: true,
            autoDismiss: true
          }),
          NgCircleProgressModule.forRoot(),
        ),
        provideTransloco(translocoOptions),
        provideTranslocoLocale({
          defaultLocale: 'en'
        }),
        provideTranslocoPersistTranslations({
          loader: HttpLoader,
          storage: { useValue: localStorage }
        }),
        provideTranslocoPersistLang({
          storage: {
            useValue: localStorage,
          },
        }),
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
        preLoad,
        Title,
        { provide: SAVER, useFactory: getSaver },
        provideHttpClient(withInterceptorsFromDi())
    ]
} as ApplicationConfig)
.catch(err => console.error(err));
