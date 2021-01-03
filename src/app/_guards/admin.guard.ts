import { Injectable } from '@angular/core';
import { CanActivate } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';

@Injectable({
  providedIn: 'root'
})
export class AdminGuard implements CanActivate {
  constructor(private accountService: AccountService, private toastr: ToastrService) {}

  canActivate(): Observable<boolean> {
    // this automaticallys subs due to being router guard
    return this.accountService.currentUser$.pipe(
      map((user: User) => {
        if (this.accountService.hasAdminRole(user)) {
          return true;
        }

        this.toastr.error('You are not authorized to view this page.');
        return false;
      })
    );
  }
}
