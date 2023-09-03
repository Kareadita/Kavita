import { HttpClient } from '@angular/common/http';
import {DestroyRef, inject, Injectable } from '@angular/core';
import {catchError, of, ReplaySubject, throwError} from 'rxjs';
import {filter, map, switchMap, tap} from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Preferences } from '../_models/preferences/preferences';
import { User } from '../_models/user';
import { Router } from '@angular/router';
import { EVENTS, MessageHubService } from './message-hub.service';
import { ThemeService } from './theme.service';
import { InviteUserResponse } from '../_models/auth/invite-user-response';
import { UserUpdateEvent } from '../_models/events/user-update-event';
import { UpdateEmailResponse } from '../_models/auth/update-email-response';
import { AgeRating } from '../_models/metadata/age-rating';
import { AgeRestriction } from '../_models/metadata/age-restriction';
import { TextResonse } from '../_types/text-response';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

export enum Role {
  Admin = 'Admin',
  ChangePassword = 'Change Password',
  Bookmark = 'Bookmark',
  Download = 'Download',
  ChangeRestriction = 'Change Restriction'
}

@Injectable({
  providedIn: 'root'
})
export class AccountService {

  private readonly destroyRef = inject(DestroyRef);
  baseUrl = environment.apiUrl;
  userKey = 'kavita-user';
  public static lastLoginKey = 'kavita-lastlogin';
  public static localeKey = 'kavita-locale';
  private currentUser: User | undefined;

  // Stores values, when someone subscribes gives (1) of last values seen.
  private currentUserSource = new ReplaySubject<User | undefined>(1);
  public currentUser$ = this.currentUserSource.asObservable();

  private hasValidLicenseSource = new ReplaySubject<boolean>(1);
  /**
   * Does the user have an active license
   */
  public hasValidLicense$ = this.hasValidLicenseSource.asObservable();

  /**
   * SetTimeout handler for keeping track of refresh token call
   */
  private refreshTokenTimeout: ReturnType<typeof setTimeout> | undefined;

  constructor(private httpClient: HttpClient, private router: Router,
    private messageHub: MessageHubService, private themeService: ThemeService) {
      messageHub.messages$.pipe(filter(evt => evt.event === EVENTS.UserUpdate),
        map(evt => evt.payload as UserUpdateEvent),
        filter(userUpdateEvent => userUpdateEvent.userName === this.currentUser?.username),
        switchMap(() => this.refreshAccount()))
        .subscribe(() => {});
    }

  hasAdminRole(user: User) {
    return user && user.roles.includes(Role.Admin);
  }

  hasChangePasswordRole(user: User) {
    return user && user.roles.includes(Role.ChangePassword);
  }

  hasChangeAgeRestrictionRole(user: User) {
    return user && user.roles.includes(Role.ChangeRestriction);
  }

  hasDownloadRole(user: User) {
    return user && user.roles.includes(Role.Download);
  }

  hasBookmarkRole(user: User) {
    return user && user.roles.includes(Role.Bookmark);
  }

  getRoles() {
    return this.httpClient.get<string[]>(this.baseUrl + 'account/roles');
  }

  deleteLicense() {
    return this.httpClient.delete<string>(this.baseUrl + 'license', TextResonse);
  }

  hasValidLicense(forceCheck: boolean = false) {
    return this.httpClient.get<string>(this.baseUrl + 'license/valid-license?forceCheck=' + forceCheck, TextResonse)
      .pipe(
        map(res => res === "true"),
        tap(res => {
          this.hasValidLicenseSource.next(res)
        }),
        catchError(error => {
          this.hasValidLicenseSource.next(false);
          return throwError(error); // Rethrow the error to propagate it further
        })
      );
  }

  hasAnyLicense() {
    return this.httpClient.get<string>(this.baseUrl + 'license/has-license', TextResonse)
      .pipe(
        map(res => res === "true"),
      );
  }

  updateUserLicense(license: string, email: string) {
  return this.httpClient.post<string>(this.baseUrl + 'license', {license, email}, TextResonse)
    .pipe(map(res => res === "true"));
  }

  login(model: {username: string, password: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/login', model).pipe(
      map((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
          this.messageHub.createHubConnection(user, this.hasAdminRole(user));
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  setCurrentUser(user?: User) {
    if (user) {
      user.roles = [];
      const roles = this.getDecodedToken(user.token).role;
      Array.isArray(roles) ? user.roles = roles : user.roles.push(roles);

      localStorage.setItem(this.userKey, JSON.stringify(user));
      localStorage.setItem(AccountService.lastLoginKey, user.username);
      if (user.preferences && user.preferences.theme) {
        this.themeService.setTheme(user.preferences.theme.name);
      } else {
        this.themeService.setTheme(this.themeService.defaultTheme);
      }
    } else {
      this.themeService.setTheme(this.themeService.defaultTheme);
    }

    this.currentUser = user;
    this.currentUserSource.next(user);

    this.stopRefreshTokenTimer();

    if (this.currentUser) {
      this.messageHub.createHubConnection(this.currentUser, this.hasAdminRole(this.currentUser));
      this.hasValidLicense().subscribe();
      this.startRefreshTokenTimer();
    }
  }

  logout() {
    localStorage.removeItem(this.userKey);
    this.currentUserSource.next(undefined);
    this.currentUser = undefined;
    this.stopRefreshTokenTimer();
    this.messageHub.stopHubConnection();
    // Upon logout, perform redirection
    this.router.navigateByUrl('/login');
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

  isEmailConfirmed() {
    return this.httpClient.get<boolean>(this.baseUrl + 'account/email-confirmed');
  }

  migrateUser(model: {email: string, username: string, password: string, sendEmail: boolean}) {
    return this.httpClient.post<string>(this.baseUrl + 'account/migrate-email', model, TextResonse);
  }

  confirmMigrationEmail(model: {email: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-migration-email', model);
  }

  resendConfirmationEmail(userId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'account/resend-confirmation-email?userId=' + userId, {}, TextResonse);
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
    return this.httpClient.post<UpdateEmailResponse>(this.baseUrl + 'account/update/email', {email, password});
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
        this.setCurrentUser(this.currentUser);

        // Update the locale on disk (for logout and compact-number pipe)
        localStorage.setItem(AccountService.localeKey, this.currentUser.preferences.locale);
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

  resetApiKey() {
    return this.httpClient.post<string>(this.baseUrl + 'account/reset-api-key', {}, TextResonse).pipe(map(key => {
      const user = this.getUserFromLocalStorage();
      if (user) {
        user.apiKey = key;

        localStorage.setItem(this.userKey, JSON.stringify(user));

        this.currentUserSource.next(user);
        this.currentUser = user;
      }
      return key;
    }));
  }

  getOpdsUrl() {
    return this.httpClient.get<string>(this.baseUrl + 'account/opds-url', TextResonse);
  }


  private refreshAccount() {
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
    if (this.currentUser === null || this.currentUser === undefined) return of();
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
