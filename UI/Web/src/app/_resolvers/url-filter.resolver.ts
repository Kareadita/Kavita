import {Injectable} from "@angular/core";
import {ActivatedRouteSnapshot, Resolve, RouterStateSnapshot} from "@angular/router";
import {Observable, of} from "rxjs";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";

/**
 * Checks the url for a filter and resolves one if applicable, otherwise returns null.
 * It is up to the consumer to cast appropriately.
 */
@Injectable({
  providedIn: 'root'
})
export class UrlFilterResolver implements Resolve<any> {

  constructor(private filterUtilitiesService: FilterUtilitiesService) {}

  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<FilterV2 | null> {
    if (!state.url.includes('?')) return of(null);
    return this.filterUtilitiesService.decodeFilter(state.url.split('?')[1]);
  }
}
