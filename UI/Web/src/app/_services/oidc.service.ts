import {DestroyRef, Injectable} from '@angular/core';
import {OAuthService} from "angular-oauth2-oidc";
import {BehaviorSubject, from} from "rxjs";
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {OidcConfig} from "../admin/_models/oidc-config";
import {AccountService} from "./account.service";
import {NavService} from "./nav.service";
import {Router} from "@angular/router";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {take} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class OidcService {

  /*
  TODO: Further cleanup, nicer handling for the user
  See: https://github.com/jeroenheijmans/sample-angular-oauth2-oidc-with-auth-guards
  Service: https://github.com/jeroenheijmans/sample-angular-oauth2-oidc-with-auth-guards/blob/master/src/app/core/auth.service.ts
   */

  baseUrl = environment.apiUrl;
  settingsSource = new BehaviorSubject<OidcConfig | null>(null);
  settings$ = this.settingsSource.asObservable();

  constructor(
    private oauth2: OAuthService,
    private httpClient: HttpClient,
    private accountService: AccountService,
    private navService: NavService,
    private router: Router,
    private destroyRef: DestroyRef,
    ) {

    this.config().subscribe(oidcSetting => {
      if (!oidcSetting.authority) {
        return
      }

      this.oauth2.configure({
        issuer: oidcSetting.authority,
        clientId: oidcSetting.clientId,
        requireHttps: oidcSetting.authority.startsWith("https://"),
        redirectUri: window.location.origin + "/oidc/callback",
        postLogoutRedirectUri: window.location.origin + "/login",
        showDebugInformation: true,
        responseType: 'code',
        scope: "openid profile email roles offline_access",
      });
      this.settingsSource.next(oidcSetting);
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
        next: success => {
          if (!success) return;

          this.tryLogin();
        },
        error: error => {
          console.log(error);
        }
      });
    })
  }

  private tryLogin() {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) return;

      if (this.token) {
        this.accountService.loginByToken(this.token).subscribe({
          next: _ => {
            this.doLogin();
          }
        });
      }
    });
  }


  oidcLogin() {
    this.oauth2.initLoginFlow();
  }

  config() {
    return this.httpClient.get<OidcConfig>(this.baseUrl + "oidc/config");
  }

  get token() {
    return this.oauth2.getAccessToken();
  }

  logout() {
    this.oauth2.logOut();
  }

  private doLogin() {
    this.navService.showNavBar();
    this.navService.showSideNav();

    // Check if user came here from another url, else send to library route
    const pageResume = localStorage.getItem('kavita--auth-intersection-url');
    if (pageResume && pageResume !== '/login') {
      localStorage.setItem('kavita--auth-intersection-url', '');
      this.router.navigateByUrl(pageResume);
    } else {
      localStorage.setItem('kavita--auth-intersection-url', '');
      this.router.navigateByUrl('/home');
    }
  }



}
