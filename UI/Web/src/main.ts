/// <reference types="@angular/localize" />
import {APP_INITIALIZER, importProvidersFrom} from '@angular/core';
import { AppComponent } from './app/app.component';
import { NgCircleProgressModule } from 'ng-circle-progress';
import { ToastrModule } from 'ngx-toastr';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppRoutingModule } from './app/app-routing.module';
import { SAVER, getSaver } from './app/shared/_providers/saver.provider';
import { Title, BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { JwtInterceptor } from './app/_interceptors/jwt.interceptor';
import { ErrorInterceptor } from './app/_interceptors/error.interceptor';
import {HTTP_INTERCEPTORS, withInterceptorsFromDi, provideHttpClient, HttpClient} from '@angular/common/http';
import {TRANSLOCO_CONFIG, TranslocoConfig, TranslocoModule, TranslocoService} from "@ngneat/transloco";
import {environment} from "./environments/environment";
import {HttpLoader, translocoLoader} from "./httpLoader";
import {
  TRANSLOCO_PERSIST_LANG_STORAGE,
  TranslocoPersistLangModule,
} from '@ngneat/transloco-persist-lang';
import {PERSIST_TRANSLATIONS_STORAGE, TranslocoPersistTranslationsModule} from "@ngneat/transloco-persist-translations";
import {TranslocoLocaleModule} from "@ngneat/transloco-locale";
import {AccountService} from "./app/_services/account.service";
import {switchMap} from "rxjs";

const disableAnimations = !('animate' in document.documentElement);

export function preloadUser(userService: AccountService, transloco: TranslocoService) {
  return function() {
    return userService.currentUser$.pipe(switchMap((user) => {
      if (user && user.preferences.locale) {
        transloco.setActiveLang(user.preferences.locale);
        return transloco.load(user.preferences.locale)
      }

      // If no user or locale is available, fallback to the default language ('en')
      const localStorageLocale = localStorage.getItem(userService.localeKey) || 'en';
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
          TranslocoModule,
          TranslocoPersistLangModule.forRoot({
            storage: {
              provide: TRANSLOCO_PERSIST_LANG_STORAGE,
              useValue: localStorage,
            },
          }),
          TranslocoLocaleModule.forRoot(),
          TranslocoPersistTranslationsModule.forRoot({
            loader: HttpLoader,
            storage: {
              provide: PERSIST_TRANSLATIONS_STORAGE,
                useValue: sessionStorage
            }
          })
        ),
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
        {
          provide: TRANSLOCO_CONFIG,
          useValue: {
            reRenderOnLangChange: true,
            availableLangs: ['en', 'es'], // TODO: Derive this from the directory
            prodMode: environment.production,
            defaultLang: 'en',
            fallbackLang: 'en',
            missingHandler: {
              useFallbackTranslation: true,
              allowEmpty: true,
            }
          } as TranslocoConfig
        },
        preLoad,
        Title,
        { provide: SAVER, useFactory: getSaver },
        provideHttpClient(withInterceptorsFromDi())
    ]
})
.catch(err => console.error(err));
