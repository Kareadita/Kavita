import {HttpClient} from '@angular/common/http';
import {computed, DestroyRef, inject, Injectable} from '@angular/core';
import {Observable, of, ReplaySubject, shareReplay} from 'rxjs';
import {filter, map, switchMap, tap} from 'rxjs/operators';
import {environment} from 'src/environments/environment';
import {Preferences} from '../_models/preferences/preferences';
import {User} from '../_models/user/user';
import {Router} from '@angular/router';
import {EVENTS, MessageHubService} from './message-hub.service';
import {InviteUserResponse} from '../_models/auth/invite-user-response';
import {UserUpdateEvent} from '../_models/events/user-update-event';
import {AgeRating} from '../_models/metadata/age-rating';
import {AgeRestriction} from '../_models/metadata/age-restriction';
import {TextResonse} from '../_types/text-response';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {Action} from "./action-factory.service";
import {LicenseService} from "./license.service";
import {LocalizationService} from "./localization.service";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {AuthKey, OpdsName} from "../_models/user/auth-key";

export enum Role {
  Admin = 'Admin',
  ChangePassword = 'Change Password',
  Bookmark = 'Bookmark',
  Download = 'Download',
  ChangeRestriction = 'Change Restriction',
  ReadOnly = 'Read Only',
  Login = 'Login',
  Promote = 'Promote',
}

export const allRoles = [
  Role.Admin,
  Role.ChangePassword,
  Role.Bookmark,
  Role.Download,
  Role.ChangeRestriction,
  Role.ReadOnly,
  Role.Login,
  Role.Promote,
]

@Injectable({
  providedIn: 'root'
})
export class AccountService {

  private readonly destroyRef = inject(DestroyRef);
  private readonly licenseService = inject(LicenseService);
  private readonly localizationService = inject(LocalizationService);
  private readonly httpClient = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly messageHub = inject(MessageHubService);

  baseUrl = environment.apiUrl;
  userKey = 'kavita-user';
  public static lastLoginKey = 'kavita-lastlogin';
  public static localeKey = 'kavita-locale';
  private currentUser: User | undefined;

  // Stores values, when someone subscribes gives (1) of last values seen.
  private currentUserSource = new ReplaySubject<User | undefined>(1);
  public currentUser$ = this.currentUserSource.asObservable().pipe(takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
  public isAdmin$: Observable<boolean> = this.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), map(u => {
    if (!u) return false;
    return this.hasAdminRole(u);
  }), shareReplay({bufferSize: 1, refCount: true}));
  public readonly isAdmin = toSignal(this.isAdmin$);

  public readonly currentUserSignal = toSignal(this.currentUser$);
  public readonly userId = computed(() => this.currentUserSignal()?.id);
  public readonly currentUserGenericApiKey = computed(() => this.currentUserSignal()?.authKeys.filter(k => k.name === OpdsName)[0].key);
  public readonly isReadOnly = computed(() => this.currentUserSignal()?.roles.includes(Role.ReadOnly) ?? true);

  /**
   * SetTimeout handler for keeping track of refresh token call
   */
  private refreshTokenTimeout: ReturnType<typeof setTimeout> | undefined;

  private isOnline: boolean = true;

  constructor() {
      this.messageHub.messages$.pipe(filter(evt => evt.event === EVENTS.UserUpdate),
        map(evt => evt.payload as UserUpdateEvent),
        filter(userUpdateEvent => userUpdateEvent.userName === this.currentUser?.username),
        switchMap(() => this.refreshAccount()))
        .subscribe(() => {});

      this.messageHub.messages$.pipe(
        filter(evt => evt.event === EVENTS.AuthKeyUpdate),
        map(evt => evt.payload as {authKey: AuthKey}),
        tap(({authKey}) => {
          const existingKeys = this.currentUser!.authKeys;
          const index = existingKeys.findIndex(k => k.id === authKey.id);

          const authKeys = index >= 0
            ? existingKeys.map(k => k.id === authKey.id ? authKey : k)
            : [...existingKeys, authKey];

          this.setCurrentUser({
            ...this.currentUser!,
            authKeys: authKeys,
          }, false);
        }),
      ).subscribe();

    this.messageHub.messages$.pipe(
      filter(evt => evt.event === EVENTS.AuthKeyDeleted),
      map(evt => evt.payload as {id: number}),
      tap(({id}) => {
        const authKeys = this.currentUser!.authKeys.filter(k => k.id !== id);

        this.setCurrentUser({
          ...this.currentUser!,
          authKeys: authKeys,
        }, false);
      }),
    ).subscribe();


    window.addEventListener("offline", (e) => {
      this.isOnline = false;
    });

    window.addEventListener("online", (e) => {
      this.isOnline = true;
      this.refreshToken().subscribe();
    });
  }

  canInvokeAction(user: User, action: Action) {
    const isAdmin = this.hasAdminRole(user);
    const canDownload = this.hasDownloadRole(user);
    const canPromote = this.hasPromoteRole(user);

    if (isAdmin) return true;
    if (action === Action.Download) return canDownload;
    if (action === Action.Promote || action === Action.UnPromote) return canPromote;
    if (action === Action.Delete) return isAdmin;
    return true;
  }

  /**
   * If the user has any role in the restricted roles array or is an Admin
   * @param user
   * @param roles
   * @param restrictedRoles
   */
  hasAnyRole(user: User, roles: Array<Role>, restrictedRoles: Array<Role> = []) {
    if (!user || !user.roles) {
      return false;
    }

    // If the user is an admin, they have the role
    if (this.hasAdminRole(user)) {
      return true;
    }

    // If restricted roles are provided and the user has any of them, deny access
    if (restrictedRoles.length > 0 && restrictedRoles.some(role => user.roles.includes(role))) {
      return false;
    }

    // If roles are empty, allow access (no restrictions by roles)
    if (roles.length === 0) {
      return true;
    }

    // Allow access if the user has any of the allowed roles
    return roles.some(role => user.roles.includes(role));
  }

  /**
   * If User or Admin, will return false
   * @param user
   * @param restrictedRoles
   */
  hasAnyRestrictedRole(user: User, restrictedRoles: Array<Role> = []) {
    if (!user || !user.roles) {
      return true;
    }

    if (restrictedRoles.length === 0) {
      return false;
    }

    // If the user is an admin, they have the role
    if (this.hasAdminRole(user)) {
      return false;
    }


    if (restrictedRoles.length > 0 && restrictedRoles.some(role => user.roles.includes(role))) {
      return true;
    }

    return false;
  }

  hasAdminRole(user: User) {
    return user && user.roles.includes(Role.Admin);
  }

  hasChangePasswordRole(user: User) {
    return user && user.roles.includes(Role.ChangePassword);
  }

  hasChangeAgeRestrictionRole(user: User) {
    return user && !user.roles.includes(Role.Admin) && user.roles.includes(Role.ChangeRestriction);
  }

  hasDownloadRole(user: User) {
    return user && user.roles.includes(Role.Download);
  }

  hasBookmarkRole(user: User) {
    return user && user.roles.includes(Role.Bookmark);
  }

  hasReadOnlyRole(user: User) {
    return user && user.roles.includes(Role.ReadOnly);
  }

  hasPromoteRole(user: User) {
    return user && user.roles.includes(Role.Promote) || user.roles.includes(Role.Admin);
  }

  getRoles() {
    return this.httpClient.get<Role[]>(this.baseUrl + 'account/roles');
  }

  /**
   * Should likes be displayed for the given annotation
   * @param annotation
   */
  showAnnotationLikes(annotation: Annotation) {
    const user = this.currentUserSignal();
    if (!user) return false;

    const shareAnnotations = user.preferences.socialPreferences.shareAnnotations;
    return this.isSocialFeatureEnabled(shareAnnotations, annotation.libraryId, annotation.ageRating);
  }

  /**
   * Checks if the given social feature is enabled in a library with associated age rating on the entity
   * @param feature
   * @param activeLibrary
   * @param ageRating
   * @private
   */
  private isSocialFeatureEnabled(feature: boolean, activeLibrary: number, ageRating: AgeRating) {
    const user = this.currentUserSignal();
    if (!user || !feature) return false;

    const socialPreferences = user.preferences.socialPreferences;

    const libraryAllowed = socialPreferences.socialLibraries.length === 0 ||
      socialPreferences.socialLibraries.includes(activeLibrary);

    if (!libraryAllowed || socialPreferences.socialMaxAgeRating === AgeRating.NotApplicable) {
      return libraryAllowed;
    }

    if (socialPreferences.socialIncludeUnknowns) {
      return socialPreferences.socialMaxAgeRating >= ageRating;
    }

    return socialPreferences.socialMaxAgeRating >= ageRating && ageRating !== AgeRating.Unknown;

  }



  login(model: {username: string, password: string, apiKey?: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/login', model).pipe(
      tap((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
        }
      })
    );
  }

  getAccount() {
    return this.httpClient.get<User>(this.baseUrl + 'account').pipe(
      tap((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  setCurrentUser(user?: User, refreshConnections = true) {

    const isSameUser = this.currentUser === user;
    if (user) {
      localStorage.setItem(this.userKey, JSON.stringify(user));
      localStorage.setItem(AccountService.lastLoginKey, user.username);
    }

    this.currentUser = user;
    this.currentUserSource.next(user);

    if (!refreshConnections) return;

    this.stopRefreshTokenTimer();

    if (this.currentUser) {
      // BUG: StopHubConnection has a promise in it, this needs to be async
      // But that really messes everything up
      if (!isSameUser) {
        this.messageHub.stopHubConnection();
        this.messageHub.createHubConnection(this.currentUser);
        this.licenseService.hasValidLicense().subscribe();
      }
      if (this.currentUser.token) {
        this.startRefreshTokenTimer();
      }
    }
  }

  logout(skipAutoLogin: boolean = false) {
    const user = this.currentUserSignal();
    if (!user) return;

    localStorage.removeItem(this.userKey);
    this.currentUserSource.next(undefined);
    this.currentUser = undefined;
    this.stopRefreshTokenTimer();
    this.messageHub.stopHubConnection();

    if (!user.token) {
      window.location.href = this.baseUrl.substring(0, environment.apiUrl.indexOf("api")) + 'oidc/logout';
      return;
    }

    this.router.navigate(['/login'], {
      queryParams: {skipAutoLogin: skipAutoLogin}
    });
  }


  /**
   * Registers the first admin on the account. Only used for that. All other registrations must occur through invite
   * @param model
   * @returns
   */
  register(model: {username: string, password: string, email: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/register', model).pipe(
      map((user: User) => {
        return user;
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  isOidcAuthenticated() {
    return this.httpClient.get<string>(this.baseUrl + 'account/oidc-authenticated', TextResonse)
      .pipe(map(res => res == "true"));
  }

  isEmailConfirmed() {
    return this.httpClient.get<boolean>(this.baseUrl + 'account/email-confirmed');
  }

  isEmailValid() {
    return this.httpClient.get<string>(this.baseUrl + 'account/is-email-valid', TextResonse)
      .pipe(map(res => res == "true"));
  }

  confirmMigrationEmail(model: {email: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-migration-email', model);
  }

  resendConfirmationEmail(userId: number) {
    return this.httpClient.post<InviteUserResponse>(this.baseUrl + 'account/resend-confirmation-email?userId=' + userId, {});
  }

  inviteUser(model: {email: string, roles: Array<string>, libraries: Array<number>, ageRestriction: AgeRestriction}) {
    return this.httpClient.post<InviteUserResponse>(this.baseUrl + 'account/invite', model);
  }

  confirmEmail(model: {email: string, username: string, password: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-email', model);
  }

  confirmEmailUpdate(model: {email: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-email-update', model);
  }

  /**
   * Given a user id, returns a full url for setting up the user account
   * @param userId
   * @param withBaseUrl Should base url be included in invite url
   * @returns
   */
  getInviteUrl(userId: number, withBaseUrl: boolean = true) {
    return this.httpClient.get<string>(this.baseUrl + 'account/invite-url?userId=' + userId + '&withBaseUrl=' + withBaseUrl, TextResonse);
  }

  getDecodedToken(token: string) {
    return JSON.parse(atob(token.split('.')[1]));
  }

  requestResetPasswordEmail(email: string) {
    return this.httpClient.post<string>(this.baseUrl + 'account/forgot-password?email=' + encodeURIComponent(email), {}, TextResonse);
  }

  confirmResetPasswordEmail(model: {email: string, token: string, password: string}) {
    return this.httpClient.post<string>(this.baseUrl + 'account/confirm-password-reset', model, TextResonse);
  }

  resetPassword(username: string, password: string, oldPassword: string) {
    return this.httpClient.post(this.baseUrl + 'account/reset-password', {username, password, oldPassword}, TextResonse);
  }

  update(model: {email: string, roles: Array<string>, libraries: Array<number>, userId: number, ageRestriction: AgeRestriction}) {
    return this.httpClient.post(this.baseUrl + 'account/update', model);
  }

  updateEmail(email: string, password: string) {
    return this.httpClient.post<InviteUserResponse>(this.baseUrl + 'account/update/email', {email, password});
  }

  updateAgeRestriction(ageRating: AgeRating, includeUnknowns: boolean) {
    return this.httpClient.post(this.baseUrl + 'account/update/age-restriction', {ageRating, includeUnknowns});
  }

  /**
   * This will get latest preferences for a user and cache them into user store
   * @returns
   */
  getPreferences() {
    return this.httpClient.get<Preferences>(this.baseUrl + 'users/get-preferences').pipe(map(pref => {
      if (this.currentUser !== undefined && this.currentUser !== null) {
        this.currentUser.preferences = pref;
        this.setCurrentUser(this.currentUser);
      }
      return pref;
    }), takeUntilDestroyed(this.destroyRef));
  }

  updatePreferences(userPreferences: Preferences) {
    return this.httpClient.post<Preferences>(this.baseUrl + 'users/update-preferences', userPreferences).pipe(map(settings => {
      if (this.currentUser !== undefined && this.currentUser !== null) {
        this.currentUser.preferences = settings;
        this.setCurrentUser(this.currentUser, false);

        // Update the locale on disk (for logout and compact-number pipe)
        localStorage.setItem(AccountService.localeKey, this.currentUser.preferences.locale);
        this.localizationService.refreshTranslations(this.currentUser.preferences.locale);

      }
      return settings;
    }), takeUntilDestroyed(this.destroyRef));
  }

  getUserFromLocalStorage(): User | undefined {

    const userString = localStorage.getItem(this.userKey);

    if (userString) {
      return JSON.parse(userString)
    }

    return undefined;
  }

  getOpdsUrl() {
    return this.httpClient.get<string>(this.baseUrl + 'account/opds-url', TextResonse);
  }

  getAuthKeys() {
    return this.httpClient.get<AuthKey[]>(this.baseUrl + `account/auth-keys`);
  }

  createAuthKey(data: {keyLength: number, name: string, expiresUtc: string | null}) {
    return this.httpClient.post(this.baseUrl + 'account/create-auth-key', data);
  }

  rotateAuthKey(id: number, data: {keyLength: number, name: string, expiresUtc: string | null}) {
    return this.httpClient.post(this.baseUrl + `account/rotate-auth-key?authKeyId=${id}`, data);
  }


  deleteAuthKey(id: number) {
    return this.httpClient.delete(this.baseUrl + `account/auth-key?authKeyId=${id}`);
  }


  refreshAccount() {
    if (this.currentUser === null || this.currentUser === undefined) return of();
    return this.httpClient.get<User>(this.baseUrl + 'account/refresh-account').pipe(map((user: User) => {
      if (user) {
        this.currentUser = {...user};
      }

      this.setCurrentUser(this.currentUser);
      return user;
    }));
  }


  private refreshToken() {
    if (this.currentUser === null || this.currentUser === undefined || !this.isOnline || !this.currentUser.token) return of();

    return this.httpClient.post<{token: string, refreshToken: string}>(this.baseUrl + 'account/refresh-token',
     {token: this.currentUser.token, refreshToken: this.currentUser.refreshToken}).pipe(map(user => {
      if (this.currentUser) {
        this.currentUser.token = user.token;
        this.currentUser.refreshToken = user.refreshToken;
      }

      this.setCurrentUser(this.currentUser);
      return user;
    }));
  }

  /**
   * Every 10 mins refresh the token
   */
  private startRefreshTokenTimer() {
    if (this.currentUser === null || this.currentUser === undefined) {
      this.stopRefreshTokenTimer();
      return;
    }

    this.stopRefreshTokenTimer();

    this.refreshTokenTimeout = setInterval(() => this.refreshToken().subscribe(() => {}), (60 * 10_000));
  }

  private stopRefreshTokenTimer() {
    if (this.refreshTokenTimeout !== undefined) {
      clearInterval(this.refreshTokenTimeout);
    }
  }

}
