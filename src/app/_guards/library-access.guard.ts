import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable } from 'rxjs';
import { MemberService } from '../_services/member.service';

@Injectable({
  providedIn: 'root'
})
export class LibraryAccessGuard implements CanActivate {

  constructor(private memberService: MemberService) {}

  canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {
    const libraryId = parseInt(state.url.split('library/')[1], 10);
    return this.memberService.hasLibraryAccess(libraryId);
  }
}
