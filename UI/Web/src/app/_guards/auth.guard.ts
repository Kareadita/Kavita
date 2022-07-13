import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  public urlKey: string = 'kavita--auth-intersection-url';
  constructor(private accountService: AccountService, private router: Router, private toastr: ToastrService) {}

  canActivate(): Observable<boolean> {
    return this.accountService.currentUser$.pipe(take(1),
      map((user) => {
        if (user) {
          return true;
        }
        if (this.toastr.toasts.filter(toast => toast.message === 'Unauthorized' || toast.message === 'You are not authorized to view this page.').length === 0) {
          this.toastr.error('You are not authorized to view this page.');
        }
        localStorage.setItem(this.urlKey, window.location.pathname);
        this.router.navigateByUrl('/login');
        return false;
      })
    );
  }
}
