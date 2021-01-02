import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';

@Injectable({
  providedIn: 'root'
})
export class LibraryAccessGuard implements CanActivate {

  constructor(private accountService: AccountService, private toastr: ToastrService, private memberService: MemberService) {}

  canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {

    const libraryId = parseInt(state.url.split('library/')[1], 10);
    return this.memberService.hasLibraryAccess(libraryId);

    // return this.accountService.currentUser$.pipe(
    //   take((user: User) => {
    //     if (user) {
    //       const libraryId = parseInt(state.url.split('library/')[1], 10);
    //       return this.memberService.hasLibraryAccess(libraryId);
    //       //return true;
    //     }
    //     this.toastr.error('You are not authorized to view this page.');
    //     return false;
    //   })
    // );
  }
  
}
