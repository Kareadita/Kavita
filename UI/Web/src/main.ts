/// <reference types="@angular/localize" />

import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import {importProvidersFrom, Injectable} from '@angular/core';
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
import {Translation, TRANSLOCO_CONFIG, TRANSLOCO_LOADER, TranslocoModule} from "@ngneat/transloco";
import {environment} from "./environments/environment";
import {httpLoader} from "./httpLoader";

const disableAnimations = !('animate' in document.documentElement);


bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule, AppRoutingModule, BrowserAnimationsModule.withConfig({ disableAnimations }), ToastrModule.forRoot({
            positionClass: 'toast-bottom-right',
            preventDuplicates: true,
            timeOut: 6000,
            countDuplicates: true,
            autoDismiss: true
        }), NgCircleProgressModule.forRoot(), TranslocoModule),
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
        httpLoader,
        {
          provide: TRANSLOCO_CONFIG,
          useValue: {
            reRenderOnLangChange: true,
            availableLangs: ['en', 'es'],
            prodMode: environment.production,
            defaultLang: 'en',
            fallbackLang: 'en',
            missingHandler: {
              useFallbackTranslation: true
            }
          }
        },
        Title,
        { provide: SAVER, useFactory: getSaver },
        provideHttpClient(withInterceptorsFromDi())
    ]
})
.catch(err => console.error(err));
