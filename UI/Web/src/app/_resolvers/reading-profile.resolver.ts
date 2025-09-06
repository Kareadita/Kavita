import { Injectable, inject } from '@angular/core';
import {ActivatedRouteSnapshot, Resolve, RouterStateSnapshot} from '@angular/router';
import {Observable} from 'rxjs';
import {ReadingProfileService} from "../_services/reading-profile.service";

@Injectable({
  providedIn: 'root'
})
export class ReadingProfileResolver implements Resolve<any> {
  private readingProfileService = inject(ReadingProfileService);


  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<any> {
    // Extract seriesId from route params or parent route
    const seriesId = route.params['seriesId'] || route.parent?.params['seriesId'];
    return this.readingProfileService.getForSeries(seriesId);
  }
}
