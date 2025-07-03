import {Injectable} from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor } from '@angular/common/http';
import {Observable, switchMap} from 'rxjs';
import { AccountService } from '../_services/account.service';
import { take } from 'rxjs/operators';
import { OidcService } from '../_services/oidc.service';

@Injectable()
export class JwtInterceptor implements HttpInterceptor {

  constructor(private accountService: AccountService, private oidcService: OidcService) { }

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return this.accountService.currentUser$.pipe(
      take(1),
      switchMap(user => {
        if (user) {
          const token = this.oidcService.hasValidToken() ? this.oidcService.token : user.token;
          request = request.clone({
            setHeaders: {
              Authorization: `Bearer ${token}`
            }
          });
        }
        return next.handle(request);
      }));
  }
}
