import {DestroyRef, effect, inject, Injectable, signal} from '@angular/core';
import {OAuthErrorEvent, OAuthService} from "angular-oauth2-oidc";
import {BehaviorSubject, from, Observable} from "rxjs";
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {OidcPublicConfig} from "../admin/_models/oidc-config";
import {AccountService} from "./account.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {take} from "rxjs/operators";
import {ToastrService} from "ngx-toastr";
import {translate} from "@jsverse/transloco";

@Injectable({
  providedIn: 'root'
})
export class OidcService {

  private readonly oauth2 = inject(OAuthService);
  private readonly httpClient = inject(HttpClient);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastR = inject(ToastrService);

  baseUrl = environment.apiUrl;

  private readonly loaded = new BehaviorSubject<boolean>(false);
  public readonly loaded$: Observable<boolean> = this.loaded.asObservable();
  private readonly _ready = signal(false);
  public readonly ready = this._ready.asReadonly();
  private readonly _settings = signal<OidcPublicConfig | undefined>(undefined);
  public readonly settings = this._settings.asReadonly();

  constructor() {
    // log events in dev
    if (!environment.production) {
      this.oauth2.events.subscribe(event => {
        if (event instanceof OAuthErrorEvent) {
          console.error('OAuthErrorEvent Object:', event);
        } else {
          console.debug('OAuthEvent Object:', event);
        }
      });
    }

    this.config().subscribe(oidcSetting => {
      if (!oidcSetting.authority) {
        this.loaded.next(true);
        return
      }

      this.oauth2.configure({
        issuer: oidcSetting.authority,
        clientId: oidcSetting.clientId,
        // Require https in production unless localhost
        requireHttps: environment.production ? 'remoteOnly' : false,
        redirectUri: window.location.origin + "/oidc/callback",
        postLogoutRedirectUri: window.location.origin + "/login",
        showDebugInformation: !environment.production,
        responseType: 'code',
        scope: "openid profile email roles offline_access",
        strictDiscoveryDocumentValidation: false,
      });
      this._settings.set(oidcSetting);
      this.oauth2.setupAutomaticSilentRefresh();

      this.oauth2.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
        if (event.type !== "token_refreshed") return;

        this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
          if (!user) return; // Don't update tokens when we're not logged in. But what's going on?

          // TODO: Do we need to refresh the SignalR connection here?
          user.oidcToken = this.token;
        });
      });

      from(this.oauth2.loadDiscoveryDocumentAndTryLogin()).subscribe({
        next: _ => {
          this.loaded.next(true);
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
    return this.httpClient.get<OidcPublicConfig>(this.baseUrl + "oidc/config");
  }

  get token() {
    return this.oauth2.getAccessToken();
  }

}
