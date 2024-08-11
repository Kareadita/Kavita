import { Injectable } from '@angular/core';
import {CanActivate, Router} from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';
import {TranslocoService} from "@jsverse/transloco";

@Injectable({
  providedIn: 'root'
})
export class AdminGuard implements CanActivate {
  constructor(private accountService: AccountService, private toastr: ToastrService,
              private router: Router,
              private translocoService: TranslocoService) {}

  canActivate(): Observable<boolean> {
    return this.accountService.currentUser$.pipe(take(1),
      map((user) => {
        if (user && this.accountService.hasAdminRole(user)) {
          return true;
        }

        this.toastr.error(this.translocoService.translate('toasts.unauthorized-1'));
        this.router.navigateByUrl('/home');
        return false;
      })
    );
  }
}
