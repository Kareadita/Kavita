import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy } from '@angular/core';
import { Observable, ReplaySubject, Subject } from 'rxjs';
import { map, takeUntil } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Preferences } from '../_models/preferences/preferences';
import { User } from '../_models/user';
import * as Sentry from "@sentry/angular";
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AccountService implements OnDestroy {

  baseUrl = environment.apiUrl;
  userKey = 'kavita-user';
  currentUser: User | undefined;

  // Stores values, when someone subscribes gives (1) of last values seen.
  private currentUserSource = new ReplaySubject<User>(1);
  currentUser$ = this.currentUserSource.asObservable();

  private readonly onDestroy = new Subject<void>();

  constructor(private httpClient: HttpClient, private router: Router) {}
  
  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  hasAdminRole(user: User) {
    return user && user.roles.includes('Admin');
  }

  hasDownloadRole(user: User) {
    return user && user.roles.includes('Download');
  }

  getRoles() {
    return this.httpClient.get<string[]>(this.baseUrl + 'account/roles');
  }

  login(model: any): Observable<any> {
    return this.httpClient.post<User>(this.baseUrl + 'account/login', model).pipe(
      map((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
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
      Sentry.setContext('admin', {'admin': this.hasAdminRole(user)});
      Sentry.configureScope(scope => {
        scope.setUser({
          username: user.username
        });
      });

      localStorage.setItem(this.userKey, JSON.stringify(user));
    }

    this.currentUserSource.next(user);
    this.currentUser = user;
  }

  logout() {
    localStorage.removeItem(this.userKey);
    this.currentUserSource.next(undefined);
    this.currentUser = undefined;
    // Upon logout, perform redirection
    this.router.navigateByUrl('/login');
  }

  register(model: {username: string, password: string, isAdmin?: boolean}) {
    if (!model.hasOwnProperty('isAdmin')) {
      model.isAdmin = false;
    }

    return this.httpClient.post<User>(this.baseUrl + 'account/register', model).pipe(
      map((user: User) => {
        return user;
      }),
      takeUntil(this.onDestroy)
    );
  }

  getDecodedToken(token: string) {
    return JSON.parse(atob(token.split('.')[1]));
  }

  resetPassword(username: string, password: string) {
    return this.httpClient.post(this.baseUrl + 'account/reset-password', {username, password}, {responseType: 'json' as 'text'});
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

}
