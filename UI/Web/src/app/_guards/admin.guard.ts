import {inject} from '@angular/core';
import {CanActivateFn, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {AccountService} from '../_services/account.service';
import {TranslocoService} from "@jsverse/transloco";

export const adminGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const toastr = inject(ToastrService);
  const router = inject(Router);
  const translocoService = inject(TranslocoService);

  if (accountService.hasAdminRole()) return true;

  toastr.error(translocoService.translate('toasts.unauthorized-1'));
  return router.createUrlTree(['/home']);
};
