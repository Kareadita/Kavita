import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy } from '@angular/core';
import { Observable, of, ReplaySubject, Subject } from 'rxjs';
import { map, takeUntil } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Preferences } from '../_models/preferences/preferences';
import { User } from '../_models/user';
import { Router } from '@angular/router';
import { MessageHubService } from './message-hub.service';

@Injectable({
  providedIn: 'root'
})
export class AccountService implements OnDestroy {

  baseUrl = environment.apiUrl;
  userKey = 'kavita-user';
  public lastLoginKey = 'kavita-lastlogin';
  currentUser: User | undefined;

  // Stores values, when someone subscribes gives (1) of last values seen.
  private currentUserSource = new ReplaySubject<User>(1);
  currentUser$ = this.currentUserSource.asObservable();

  /**
   * SetTimeout handler for keeping track of refresh token call
   */
  private refreshTokenTimeout: ReturnType<typeof setTimeout> | undefined;

  private readonly onDestroy = new Subject<void>();

  constructor(private httpClient: HttpClient, private router: Router, 
    private messageHub: MessageHubService) {}
  
  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  hasAdminRole(user: User) {
    return user && user.roles.includes('Admin');
  }

  hasChangePasswordRole(user: User) {
    return user && user.roles.includes('Change Password');
  }

  hasDownloadRole(user: User) {
    return user && user.roles.includes('Download');
  }

  getRoles() {
    return this.httpClient.get<string[]>(this.baseUrl + 'account/roles');
  }

  login(model: {username: string, password: string}): Observable<any> {
    return this.httpClient.post<User>(this.baseUrl + 'account/login', model).pipe(
      map((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
          this.messageHub.createHubConnection(user, this.hasAdminRole(user));
        }
      }),
      takeUntil(this.onDestroy)
    );
  }

  setCurrentUser(user?: User) {
    if (user) {
      user.roles = [];
      const roles = this.getDecodedToken(user.token).role;
      Array.isArray(roles) ? user.roles = roles : user.roles.push(roles);

      localStorage.setItem(this.userKey, JSON.stringify(user));
      localStorage.setItem(this.lastLoginKey, user.username);
    }

    this.currentUserSource.next(user);
    this.currentUser = user;
    if (this.currentUser !== undefined) {
      this.startRefreshTokenTimer();
    } else {
      this.stopRefreshTokenTimer();
    }
  }

  logout() {
    localStorage.removeItem(this.userKey);
    this.currentUserSource.next(undefined);
    this.currentUser = undefined;
    this.stopRefreshTokenTimer();
    // Upon logout, perform redirection
    this.router.navigateByUrl('/login');
    this.messageHub.stopHubConnection();
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
      takeUntil(this.onDestroy)
    );
  }

  migrateUser(model: {email: string, username: string, password: string, sendEmail: boolean}) {
    return this.httpClient.post<string>(this.baseUrl + 'account/migrate-email', model, {responseType: 'text' as 'json'});
  }

  confirmMigrationEmail(model: {email: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-migration-email', model);
  }

  resendConfirmationEmail(userId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'account/resend-confirmation-email?userId=' + userId, {}, {responseType: 'text' as 'json'});
  }

  inviteUser(model: {email: string, roles: Array<string>, libraries: Array<number>, sendEmail: boolean}) {
    return this.httpClient.post<string>(this.baseUrl + 'account/invite', model, {responseType: 'text' as 'json'});
  }

  confirmEmail(model: {email: string, username: string, password: string, token: string}) {
    return this.httpClient.post<User>(this.baseUrl + 'account/confirm-email', model);
  }

  getDecodedToken(token: string) {
    return JSON.parse(atob(token.split('.')[1]));
  }

  requestResetPasswordEmail(email: string) {
    return this.httpClient.post<string>(this.baseUrl + 'account/forgot-password?email=' + encodeURIComponent(email), {}, {responseType: 'text' as 'json'});
  }

  confirmResetPasswordEmail(model: {email: string, token: string, password: string}) {
    return this.httpClient.post(this.baseUrl + 'account/confirm-password-reset', model);
  }

  resetPassword(username: string, password: string) {
    return this.httpClient.post(this.baseUrl + 'account/reset-password', {username, password}, {responseType: 'json' as 'text'});
  }

  update(model: {email: string, roles: Array<string>, libraries: Array<number>, userId: number}) {
    return this.httpClient.post(this.baseUrl + 'account/update', model);
  }

  updatePreferences(userPreferences: Preferences) {
    return this.httpClient.post<Preferences>(this.baseUrl + 'users/update-preferences', userPreferences).pipe(map(settings => {
      if (this.currentUser !== undefined || this.currentUser != null) {
        this.currentUser.preferences = settings;
        this.setCurrentUser(this.currentUser);
      }
      return settings;
    }), takeUntil(this.onDestroy));
  }

  getUserFromLocalStorage(): User | undefined {

    const userString = localStorage.getItem(this.userKey);
    
    if (userString) {
      return JSON.parse(userString)
    };

    return undefined;
  }

  resetApiKey() {
    return this.httpClient.post<string>(this.baseUrl + 'account/reset-api-key', {}, {responseType: 'text' as 'json'}).pipe(map(key => {
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

  private refreshToken() {
    if (this.currentUser === null || this.currentUser === undefined) return of();

    return this.httpClient.post<{token: string, refreshToken: string}>(this.baseUrl + 'account/refresh-token', {token: this.currentUser.token, refreshToken: this.currentUser.refreshToken}).pipe(map(user => {
      if (this.currentUser) {
        this.currentUser.token = user.token;
        this.currentUser.refreshToken = user.refreshToken;
      }
      
      this.currentUserSource.next(this.currentUser);
      this.startRefreshTokenTimer();
      return user;
    }));
  }

  private startRefreshTokenTimer() {
    if (this.currentUser === null || this.currentUser === undefined) return;

    if (this.refreshTokenTimeout !== undefined) {
      this.stopRefreshTokenTimer();
    }

    const jwtToken = JSON.parse(atob(this.currentUser.token.split('.')[1]));
    // set a timeout to refresh the token a minute before it expires
    const expires = new Date(jwtToken.exp * 1000);
    const timeout = expires.getTime() - Date.now() - (60 * 1000);
    this.refreshTokenTimeout = setTimeout(() => this.refreshToken().subscribe(() => {
      console.log('Token Refreshed');
    }), timeout);
  }

  private stopRefreshTokenTimer() {
    if (this.refreshTokenTimeout !== undefined) {
      clearTimeout(this.refreshTokenTimeout);
    }
  }



}
