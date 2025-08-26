import {inject, Injectable} from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';
import {TranslocoService} from "@jsverse/transloco";
import {APP_BASE_HREF} from "@angular/common";

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  public static urlKey: string = 'kavita--auth-intersection-url';

  baseURL = inject(APP_BASE_HREF);

  constructor(private accountService: AccountService,
              private router: Router,
              private toastr: ToastrService,
              private translocoService: TranslocoService) {}

  canActivate(): Observable<boolean> {
    return this.accountService.currentUser$.pipe(take(1),
      map((user) => {
        if (user) {
          return true;
        }

        const path = window.location.pathname;
        if (path !== '/login' && !path.startsWith(this.baseURL + "registration") && path !== '') {
          localStorage.setItem(AuthGuard.urlKey, path);
        }
        this.router.navigateByUrl('/login');
        return false;
      })
    );
  }
}
