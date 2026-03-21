import {HttpClient, httpResource} from '@angular/common/http';
import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {Observable, of} from 'rxjs';
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
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {LicenseService} from "./license.service";
import {LocalizationService} from "./localization.service";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {AuthKey, ImageOnlyName, OpdsName} from "../_models/user/auth-key";
import {Action} from "../_models/actionables/action";

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
  public static userKey = 'kavita-user';
  public static lastLoginKey = 'kavita-lastlogin';
  public static localeKey = 'kavita-locale';

  private readonly _currentUser = signal<User | undefined>(undefined);
  public readonly currentUser = this._currentUser.asReadonly();

  // Derived signals
  public readonly isLoggedIn = computed(() => this._currentUser() !== undefined);
  public readonly userId = computed(() => this._currentUser()?.id);
  public readonly username = computed(() => this._currentUser()?.username);
  public readonly userPreferences = computed(() => this._currentUser()?.preferences);

  // Role signals
  public readonly hasAdminRole = computed(() => this.hasRole(this._currentUser(), Role.Admin));
  public readonly hasChangePasswordRole = computed(() => this.hasRole(this._currentUser(), Role.ChangePassword));
  public readonly hasChangeAgeRestrictionRole = computed(() => this.hasRole(this._currentUser(), Role.ChangeRestriction));
  public readonly hasDownloadRole = computed(() => this.hasRole(this._currentUser(), Role.Download));
  public readonly hasBookmarkRole = computed(() => this.hasRole(this._currentUser(), Role.Bookmark));
  public readonly hasReadOnlyRole = computed(() => this._currentUser() ? this.hasRole(this._currentUser(), Role.ReadOnly) : true);
  public readonly hasPromoteRole = computed(() => this.hasRole(this._currentUser(), Role.Promote) || this.hasRole(this._currentUser(), Role.Admin));

  public readonly currentUserGenericApiKey = computed(() =>
    this._currentUser()?.authKeys?.find(k => k.name === OpdsName)?.key
  );
  public readonly currentUserImageAuthKey = computed(() =>
    this._currentUser()?.authKeys?.find(k => k.name === ImageOnlyName)?.key
  );

  /**
   * SetTimeout handler for keeping track of refresh token call
   */
  private refreshTokenTimeout: ReturnType<typeof setTimeout> | undefined;

  private isOnline: boolean = true;

  constructor() {
      this.messageHub.messages$.pipe(filter(evt => evt.event === EVENTS.UserUpdate),
        map(evt => evt.payload as UserUpdateEvent),
        filter(userUpdateEvent => userUpdateEvent.userName === this._currentUser()?.username),
        switchMap(() => this.refreshAccount()))
        .subscribe(() => {});

      this.messageHub.messages$.pipe(
        filter(evt => evt.event === EVENTS.AuthKeyUpdate),
        map(evt => evt.payload as {authKey: AuthKey}),
        tap(({authKey}) => {
          const existing = this._currentUser();
          if (!existing) return;
          const existingKeys = existing.authKeys ?? [];
          const index = existingKeys.findIndex(k => k.id === authKey.id);
          const authKeys = index >= 0
            ? existingKeys.map(k => k.id === authKey.id ? authKey : k)
            : [...existingKeys, authKey];
          this.setCurrentUser({ ...existing, authKeys }, false);
        }),
      ).subscribe();

    this.messageHub.messages$.pipe(
      filter(evt => evt.event === EVENTS.AuthKeyDeleted),
      map(evt => evt.payload as {id: number}),
      tap(({id}) => {
        const existing = this._currentUser();
        if (!existing) return;
        this.setCurrentUser({ ...existing, authKeys: (existing.authKeys ?? []).filter(k => k.id !== id) }, false);
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

  canCurrentUserInvokeAction(action: Action) {
    const user = this.currentUser();
    if (!user) return false;

    return this.canInvokeAction(user, action);
  }

  canInvokeAction(user: User, action: Action) {
    const isAdmin = this.hasRole(user, Role.Admin);
    const canDownload = this.hasRole(user, Role.Download);
    const canPromote = this.hasRole(user, Role.Promote) || this.hasRole(user, Role.Admin);

    if (isAdmin) return true;
    if (action === Action.Download) return canDownload;
    if (action === Action.Promote || action === Action.UnPromote) return canPromote;
    if (action === Action.Delete) return isAdmin;
    return true;
  }

  hasRole(user: User | undefined, role: Role) {
    return !!user && user.roles.includes(role);
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
    if (this.hasRole(user, Role.Admin)) {
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
   * Should likes be displayed for the given annotation
   * @param annotation
   */
  showAnnotationLikes(annotation: Annotation) {
    const user = this._currentUser();
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
    const user = this._currentUser();
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

  /** Omit auth keys since they are long-lived. The auth keys will be set from refreshAccount() */
  private getPersistableUser(user: User): Omit<User, 'authKeys'> {
    const { authKeys, ...persistable } = user;
    return persistable;
  }

  setCurrentUser(user?: User, refreshConnections = true) {
    const currentUser = this._currentUser();
    const isSameUser = !!currentUser && !!user && currentUser.username === user.username;

    if (user) {
      localStorage.setItem(AccountService.userKey, JSON.stringify(this.getPersistableUser(user)));
      localStorage.setItem(AccountService.lastLoginKey, user.username);
    }

    this._currentUser.set(user);

    if (!refreshConnections) return;

    this.stopRefreshTokenTimer();

    if (user && !isSameUser) {
      this.messageHub.stopHubConnection();
      this.messageHub.createHubConnection(user);
      this.licenseService.checkForValidLicense().subscribe();
    }

    if (user?.token) {
      this.startRefreshTokenTimer();
    }
  }

  logout(skipAutoLogin: boolean = false, skipOidcLogout: boolean = false) {
    const user = this._currentUser();
    if (!user) return;

    localStorage.removeItem(AccountService.userKey);
    this._currentUser.set(undefined);
    this.stopRefreshTokenTimer();
    this.messageHub.stopHubConnection();

    if (!skipOidcLogout && !user.token) {
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

  clearOidcLink() {
    return this.httpClient.post(this.baseUrl + 'account/clear-oidc-link', {});
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
      const current = this._currentUser();
      if (current) this.setCurrentUser({ ...current, preferences: pref });
      return pref;
    }), takeUntilDestroyed(this.destroyRef));
  }

  updatePreferences(userPreferences: Preferences) {
    return this.httpClient.post<Preferences>(this.baseUrl + 'users/update-preferences', userPreferences).pipe(map(settings => {
      const current = this._currentUser();
      if (current) {
        const localeChange = current.preferences.locale != settings.locale;
        this.setCurrentUser({ ...current, preferences: settings }, false);

        if (localeChange) {
          // Update the locale on disk (for logout and compact-number pipe)
          localStorage.setItem(AccountService.localeKey, settings.locale);
          this.localizationService.refreshTranslations(settings.locale);
        }
      }
      return settings;
    }), takeUntilDestroyed(this.destroyRef));
  }

  getUserFromLocalStorage(): User | undefined {
    const userString = localStorage.getItem(AccountService.userKey);

    if (userString) {
      return JSON.parse(userString)
    }

    return undefined;
  }

  opdsUrlRsc(keyName: () => string) {
    return httpResource.text<string>(() =>this.baseUrl + 'account/opds-url?authKeyName=' + keyName());
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


  refreshAccount(): Observable<null | User> {
    if (!this._currentUser()) return of(null);
    return this.httpClient.get<User>(this.baseUrl + 'account/refresh-account').pipe(map((user: User) => {
      if (user) {
        this.setCurrentUser({...user});
        this.licenseService.checkForValidLicense().subscribe();
      }
      return user;
    }));
  }


  private refreshToken() {
    const current = this._currentUser();
    if (!current || !this.isOnline || !current.token) return of();

    return this.httpClient.post<{token: string, refreshToken: string}>(this.baseUrl + 'account/refresh-token',
     {token: current.token, refreshToken: current.refreshToken}).pipe(map(tokens => {
      const updated = this._currentUser();
      if (updated) this.setCurrentUser({ ...updated, token: tokens.token, refreshToken: tokens.refreshToken });
      return tokens;
    }));
  }

  /**
   * Every 10 mins refresh the token
   */
  private startRefreshTokenTimer() {
    if (!this._currentUser()) {
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
