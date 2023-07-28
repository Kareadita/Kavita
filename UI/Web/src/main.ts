/// <reference types="@angular/localize" />
import {importProvidersFrom} from '@angular/core';
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
import {TRANSLOCO_CONFIG, TranslocoConfig, TranslocoModule} from "@ngneat/transloco";
import {environment} from "./environments/environment";
import {HttpLoader, translocoLoader} from "./httpLoader";
import {
  TRANSLOCO_PERSIST_LANG_STORAGE,
  TranslocoPersistLangModule,
} from '@ngneat/transloco-persist-lang';
import {PERSIST_TRANSLATIONS_STORAGE, TranslocoPersistTranslationsModule} from "@ngneat/transloco-persist-translations";
import {TranslocoLocaleModule} from "@ngneat/transloco-locale";

const disableAnimations = !('animate' in document.documentElement);


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
                useValue: localStorage
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
        Title,
        { provide: SAVER, useFactory: getSaver },
        provideHttpClient(withInterceptorsFromDi())
    ]
})
.catch(err => console.error(err));
