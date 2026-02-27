import {inject} from '@angular/core';
import {CanActivateFn, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {AccountService} from '../_services/account.service';
import {TranslocoService} from "@jsverse/transloco";
import {APP_BASE_HREF} from "@angular/common";

export const AUTH_URL_KEY = 'kavita--auth-intersection-url';

export const authGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const router = inject(Router);
  const toastr = inject(ToastrService);
  const translocoService = inject(TranslocoService);
  const baseURL = inject(APP_BASE_HREF);

  if (accountService.isLoggedIn()) return true;

  const path = window.location.pathname;
  if (path !== '/login' && !path.startsWith(baseURL + 'registration') && path !== '') {
    localStorage.setItem(AUTH_URL_KEY, path);
  }
  toastr.error(translocoService.translate('toasts.unauthorized-1'));
  return router.createUrlTree(['/login']);
};
