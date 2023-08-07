import { Injectable } from '@angular/core';
import { CanActivate } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';
import {TranslocoService} from "@ngneat/transloco";

@Injectable({
  providedIn: 'root'
})
export class AdminGuard implements CanActivate {
  constructor(private accountService: AccountService, private toastr: ToastrService,
              private translocoService: TranslocoService) {}

  canActivate(): Observable<boolean> {
    // this automatically subs due to being router guard
    return this.accountService.currentUser$.pipe(take(1),
      map((user) => {
        if (user && this.accountService.hasAdminRole(user)) {
          return true;
        }

        this.toastr.error(this.translocoService.translate('toasts.unauthorized-1'));
        return false;
      })
    );
  }
}
