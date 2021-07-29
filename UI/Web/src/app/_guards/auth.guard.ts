import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  public urlKey: string = 'kavita--auth-intersection-url';
  constructor(private accountService: AccountService, private router: Router, private toastr: ToastrService) {}

  canActivate(): Observable<boolean> {
    return this.accountService.currentUser$.pipe(take(1),
      map((user: User) => {
        if (user) {
          return true;
        }
        this.toastr.error('You are not authorized to view this page.');
        localStorage.setItem(this.urlKey, window.location.pathname);
        this.router.navigateByUrl('/libraries');
        return false;
      })
    );
  }
}
