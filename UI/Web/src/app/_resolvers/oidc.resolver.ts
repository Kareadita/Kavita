import {ActivatedRouteSnapshot, Resolve, RouterStateSnapshot} from '@angular/router';
import {Injectable} from "@angular/core";
import {Observable, take} from "rxjs";
import {OidcService} from "../_services/oidc.service";

@Injectable({
  providedIn: 'root'
})
export class OidcResolver implements Resolve<any> {

  constructor(private oidcService: OidcService) {}

  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<any> {
    return this.oidcService.loaded$.pipe(take(1));
  }
}
