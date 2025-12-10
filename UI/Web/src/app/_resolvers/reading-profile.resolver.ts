import {inject} from '@angular/core';
import {ActivatedRouteSnapshot, ResolveFn, RouterStateSnapshot} from '@angular/router';
import {Observable} from 'rxjs';
import {ReadingProfileService} from "../_services/reading-profile.service";
import {ReadingProfile} from "../_models/preferences/reading-profiles";

export const readingProfileResolver: ResolveFn<ReadingProfile> = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot
): Observable<ReadingProfile> => {
  const readingProfileService = inject(ReadingProfileService);

  // Extract seriesId from route params or parent route
  const seriesId = route.params['seriesId'] || route.parent?.params['seriesId'];
  return readingProfileService.getForSeries(seriesId);
};
