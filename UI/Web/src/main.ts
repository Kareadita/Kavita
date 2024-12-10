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
import { Title, BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { JwtInterceptor } from './app/_interceptors/jwt.interceptor';
import { ErrorInterceptor } from './app/_interceptors/error.interceptor';
import { HTTP_INTERCEPTORS, withInterceptorsFromDi, provideHttpClient } from '@angular/common/http';
import {
    provideTransloco, TranslocoConfig,
    TranslocoService
} from "@jsverse/transloco";
import {environment} from "./environments/environment";
import {HttpLoader} from "./httpLoader";
import {
  provideTranslocoPersistLang,
} from "@jsverse/transloco-persist-lang";
import {AccountService} from "./app/_services/account.service";
import {switchMap} from "rxjs";
import {provideTranslocoLocale} from "@jsverse/transloco-locale";
import {provideTranslocoPersistTranslations} from "@jsverse/transloco-persist-translations";
import {LazyLoadImageModule} from "ng-lazyload-image";
import {getSaver, SAVER} from "./app/_providers/saver.provider";
import {distinctUntilChanged} from "rxjs/operators";
import {APP_BASE_HREF, PlatformLocation} from "@angular/common";

const disableAnimations = !('animate' in document.documentElement);

export function preloadUser(userService: AccountService, transloco: TranslocoService) {
  return function() {
    return userService.currentUser$.pipe(distinctUntilChanged(), switchMap((user) => {
      if (user && user.preferences.locale) {
        transloco.setActiveLang(user.preferences.locale);
        return transloco.load(user.preferences.locale)
      }

      // If no user or locale is available, fallback to the default language ('en')
      const localStorageLocale = localStorage.getItem(AccountService.localeKey) || 'en';
      transloco.setActiveLang(localStorageLocale);
      return transloco.load(localStorageLocale);
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
  'af', 'af_ZA', 'ar', 'ar_AE', 'ar_BH', 'ar_DZ', 'ar_EG', 'ar_IQ', 'ar_JO', 'ar_KW',
  'ar_LB', 'ar_LY', 'ar_MA', 'ar_OM', 'ar_QA', 'ar_SA', 'ar_SY', 'ar_TN', 'ar_YE',
  'az', 'az_AZ', 'az_AZ', 'be', 'be_BY', 'bg', 'bg_BG', 'bs_BA', 'ca', 'ca_ES', 'cs',
  'cs_CZ', 'cy', 'cy_GB', 'da', 'da_DK', 'de', 'de_AT', 'de_CH', 'de_DE', 'de_LI', 'de_LU',
  'dv', 'dv_MV', 'el', 'el_GR', 'en', 'en_AU', 'en_BZ', 'en_CA', 'en_CB', 'en_GB', 'en_IE',
  'en_JM', 'en_NZ', 'en_PH', 'en_TT', 'en_US', 'en_ZA', 'en_ZW', 'eo', 'es', 'es_AR', 'es_BO',
  'es_CL', 'es_CO', 'es_CR', 'es_DO', 'es_EC', 'es_ES', 'es_ES', 'es_GT', 'es_HN', 'es_MX',
  'es_NI', 'es_PA', 'es_PE', 'es_PR', 'es_PY', 'es_SV', 'es_UY', 'es_VE', 'et', 'et_EE',
  'eu', 'eu_ES', 'fa', 'fa_IR', 'fi', 'fi_FI', 'fo', 'fo_FO', 'fr', 'fr_BE', 'fr_CA',
  'fr_CH', 'fr_FR', 'fr_LU', 'fr_MC', 'gl', 'gl_ES', 'gu', 'gu_IN', 'he', 'he_IL', 'hi',
  'hi_IN', 'hr', 'hr_BA', 'hr_HR', 'hu', 'hu_HU', 'hy', 'hy_AM', 'id', 'id_ID', 'is',
  'is_IS', 'it', 'it_CH', 'it_IT', 'ja', 'ja_JP', 'ka', 'ka_GE', 'kk', 'kk_KZ', 'kn',
  'kn_IN', 'ko', 'ko_KR', 'kok', 'kok_IN', 'ky', 'ky_KG', 'lt', 'lt_LT', 'lv', 'lv_LV',
  'mi', 'mi_NZ', 'mk', 'mk_MK', 'mn', 'mn_MN', 'mr', 'mr_IN', 'ms', 'ms_BN', 'ms_MY',
  'mt', 'mt_MT', 'nb', 'nb_NO', 'nl', 'nl_BE', 'nl_NL', 'nn_NO', 'ns', 'ns_ZA', 'pa',
  'pa_IN', 'pl', 'pl_PL', 'ps', 'ps_AR', 'pt', 'pt_BR', 'pt_PT', 'qu', 'qu_BO', 'qu_EC',
  'qu_PE', 'ro', 'ro_RO', 'ru', 'ru_RU', 'sa', 'sa_IN', 'se', 'se_FI', 'se_FI', 'se_FI',
  'se_NO', 'se_NO', 'se_NO', 'se_SE', 'se_SE', 'se_SE', 'sk', 'sk_SK', 'sl', 'sl_SI',
  'sq', 'sq_AL', 'sr_BA', 'sr_BA', 'sr_SP', 'sr_SP', 'sv', 'sv_FI', 'sv_SE', 'sw', 'sw_KE',
  'syr', 'syr_SY', 'ta', 'ta_IN', 'te', 'te_IN', 'th', 'th_TH', 'tl', 'tl_PH', 'tn',
  'tn_ZA', 'tr', 'tr_TR', 'tt', 'tt_RU', 'ts', 'uk', 'uk_UA', 'ur', 'ur_PK', 'uz',
  'uz_UZ', 'uz_UZ', 'vi', 'vi_VN', 'xh', 'xh_ZA', 'zh', 'zh_CN', 'zh_HK', 'zh_MO',
  'zh_SG', 'zh_TW', 'zu', 'zu_ZA', 'zh_Hans', 'zh_Hant', 'nb_NO', 'ga'
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
    },
  } as TranslocoConfig
};

function getBaseHref(platformLocation: PlatformLocation): string {
  return platformLocation.getBaseHrefFromDOM();
}

bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule,
          AppRoutingModule,
          BrowserAnimationsModule.withConfig({ disableAnimations }),
          LazyLoadImageModule,
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
          storage: { useValue: localStorage },
          ttl: 604800
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
        {
          provide: APP_BASE_HREF,
          useFactory: getBaseHref,
          deps: [PlatformLocation]
        },
        provideHttpClient(withInterceptorsFromDi())
    ]
} as ApplicationConfig)
.catch(err => console.error(err));
