import {CanActivateFn, RedirectCommand, Router} from '@angular/router';
import {AccountService} from "../_services/account.service";
import {inject} from "@angular/core";
import {MemberService} from "../_services/member.service";
import {ToastrService} from "ngx-toastr";
import {translate} from "@jsverse/transloco";
import {tap} from "rxjs";
import {map} from "rxjs/operators";

export const profileGuard: CanActivateFn = (route, state) => {
  const userId = parseInt(route.params['userId'] || route.parent?.params['userId'], 10);

  const accountService = inject(AccountService);
  const memberService = inject(MemberService);
  const router = inject(Router);
  const toastr = inject(ToastrService);

  // If this is my profile, allow
  if (accountService.currentUserSignal()?.id === userId) {
    return true;
  }

  // Otherwise check if that user has their account shared
  return memberService.hasProfileShared(userId).pipe(
    tap(hasAccess => {
      if (!hasAccess) {
        toastr.info(translate('toasts.profile-unauthorized'));
      }
    }),
    map(hasAccess => {
      if (hasAccess) {
        return true;
      }

      const dashboard = router.parseUrl('/home');
      return new RedirectCommand(dashboard);
    })
  );
};
