import {DestroyRef, effect, inject, Injectable, signal} from '@angular/core';
import {OAuthErrorEvent, OAuthService} from "angular-oauth2-oidc";
import {BehaviorSubject, from, Observable} from "rxjs";
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {OidcPublicConfig} from "../admin/_models/oidc-config";
import {AccountService} from "./account.service";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {take} from "rxjs/operators";
import {ToastrService} from "ngx-toastr";
import {translate} from "@jsverse/transloco";
import {APP_BASE_HREF} from "@angular/common";

@Injectable({
  providedIn: 'root'
})
export class OidcService {

  private readonly oauth2 = inject(OAuthService);
  private readonly httpClient = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastR = inject(ToastrService);

  protected readonly baseUrl = inject(APP_BASE_HREF);
  apiBaseUrl = environment.apiUrl;

  /**
   * True when the OIDC discovery document has been loaded, and login tried. Or no OIDC has been set up
   */
  private readonly _loaded = signal(false);
  public readonly loaded = this._loaded.asReadonly();
  public readonly loaded$ = toObservable(this.loaded);

  /**
   * OIDC discovery document has been loaded, and login tried and OIDC has been set up
   */
  private readonly _ready = signal(false);
  public readonly ready = this._ready.asReadonly();

  /**
   * Public OIDC settings
   */
  private readonly _settings = signal<OidcPublicConfig | undefined>(undefined);
  public readonly settings = this._settings.asReadonly();

  constructor() {
    // log events in dev
    if (!environment.production) {
      this.oauth2.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
        if (event instanceof OAuthErrorEvent) {
          console.error('OAuthErrorEvent Object:', event);
        } else {
          console.debug('OAuthEvent Object:', event);
        }
      });
    }

    this.config().subscribe(oidcSetting => {
      if (!oidcSetting.authority) {
        this._loaded.set(true);
        return
      }

      this.oauth2.configure({
        issuer: oidcSetting.authority,
        clientId: oidcSetting.clientId,
        // Require https in production unless localhost
        requireHttps: environment.production ? 'remoteOnly' : false,
        redirectUri: window.location.origin + this.baseUrl + "oidc/callback",
        postLogoutRedirectUri: window.location.origin + this.baseUrl + "login",
        showDebugInformation: !environment.production,
        responseType: 'code',
        scope: "openid profile email roles offline_access",
        // Not all OIDC providers follow this nicely
        strictDiscoveryDocumentValidation: false,
      });
      this._settings.set(oidcSetting);
      this.oauth2.setupAutomaticSilentRefresh();

      from(this.oauth2.loadDiscoveryDocumentAndTryLogin()).subscribe({
        next: _ => {
          this._loaded.set(true);
          this._ready.set(true);
        },
        error: error => {
          console.log(error);
          this.toastR.error(translate("oidc.error-loading-info"))
        }
      });
    })
  }


  login() {
    this.oauth2.initLoginFlow();
  }

  logout() {
    if (this.token) {
      this.oauth2.logOut();
    }
  }

  config() {
    return this.httpClient.get<OidcPublicConfig>(this.apiBaseUrl + "oidc/config");
  }

  get token() {
    return this.oauth2.getAccessToken();
  }

  hasValidToken() {
    return this.oauth2.hasValidAccessToken();
  }

}
