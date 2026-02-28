import {inject} from '@angular/core';
import {CanActivateFn, Router} from '@angular/router';
import {of} from 'rxjs';
import {MemberService} from '../_services/member.service';
import {map} from "rxjs/operators";

export const libraryAccessGuard: CanActivateFn = (route, state) => {
  const memberService = inject(MemberService);
  const router = inject(Router);

  const libraryId = parseInt(
    route.parent?.paramMap.get('libraryId') ?? route.paramMap.get('libraryId') ?? '',
    10
  );

  if (isNaN(libraryId)) {
    return of(router.parseUrl('/home'));
  }

  return memberService.hasLibraryAccess(libraryId).pipe(
    map(hasAccess => hasAccess || router.parseUrl('/home'))
  );
};
