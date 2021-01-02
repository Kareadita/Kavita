import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';

@Injectable({
  providedIn: 'root'
})
export class LibraryAccessGuard implements CanActivate {

  constructor(private accountService: AccountService, private toastr: ToastrService, private memberService: MemberService) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {





    return this.accountService.currentUser$.pipe(
      map((user: User) => {
        if (user) {
          const libraryId = parseInt(state.url.split('library/')[1], 10);
          this.memberService.hasLibraryAccess(libraryId).pipe(res => {
            console.log('return: ', res);
            return res;
          });
          console.log('state:', state.url);
          console.log('route: ', route);
          return true;
        }
        this.toastr.error('You are not authorized to view this page.');
        return false;
      })
    );
  }
  
}
