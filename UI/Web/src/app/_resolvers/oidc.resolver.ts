import {ActivatedRouteSnapshot, Resolve, Router, RouterStateSnapshot} from '@angular/router';
import {inject, Injectable} from "@angular/core";
import {catchError, filter, Observable, of, take, timeout} from "rxjs";
import {OidcService} from "../_services/oidc.service";
import {ToastrService} from "ngx-toastr";

@Injectable({
  providedIn: 'root'
})
export class OidcResolver implements Resolve<any> {

  private oidcService = inject(OidcService);
  private toastR = inject(ToastrService);

  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<any> {
    return this.oidcService.loaded$.pipe(
      filter(value => value),
      take(1),
      timeout(5000),
      catchError(err => {
        console.log(err);
        this.toastR.error("oidc.timeout");
        return of(true);
    }));
  }
}
