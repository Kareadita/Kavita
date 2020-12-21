import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, ReplaySubject } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { User } from '../_models/user';

@Injectable({
  providedIn: 'root'
})
export class AccountService {

  baseUrl = environment.apiUrl;
  userKey = 'kavita-user';

  // Stores values, when someone subscribes gives (1) of last values seen.
  private currentUserSource = new ReplaySubject<User>(1);
  currentUser$ = this.currentUserSource.asObservable(); // $ at end is because this is observable

  constructor(private httpClient: HttpClient) { 
  }

  login(model: any): Observable<any> {
    return this.httpClient.post<User>(this.baseUrl + 'account/login', model).pipe(
      map((response: User) => {
        const user = response;
        if (user) {
          this.setCurrentUser(user);
        }
      })
    );
  }

  setCurrentUser(user: User) {
    localStorage.setItem(this.userKey, JSON.stringify(user));
    this.currentUserSource.next(user);
  }

  logout() {
    localStorage.removeItem(this.userKey);
    this.currentUserSource.next(undefined);
  }

  register(model: {username: string, password: string, isAdmin?: boolean}, login = false) {
    if (model?.isAdmin) {
      model.isAdmin = false;
    }

    return this.httpClient.post<User>(this.baseUrl + 'account/register', model).pipe(
      map((user: User) => {
        return user;
      })
    );
  }


}
