import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of} from "rxjs";
import {SeriesService} from "../_services/series.service";
import {Series} from "../_models/series";


export const seriesResolver: ResolveFn<Series | UrlTree> = (route, state) => {
  const seriesService = inject(SeriesService);
  const router = inject(Router);

  const seriesId = route.parent?.paramMap.get('seriesId') || route.paramMap.get('seriesId');

  if (!seriesId || seriesId === '0') {
    console.error('Series ID not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return seriesService.getSeries(parseInt(seriesId, 10)).pipe(
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
