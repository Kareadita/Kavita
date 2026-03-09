import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of} from "rxjs";
import {Volume} from "../_models/volume";
import {VolumeService} from "../_services/volume.service";

export const volumeResolver: ResolveFn<Volume | UrlTree> = (route, state) => {
  const volumeService = inject(VolumeService);
  const router = inject(Router);

  const volumeId = route.parent?.paramMap.get('volumeId') || route.paramMap.get('volumeId');

  if (!volumeId || volumeId === '0') {
    console.error('Volume ID not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return volumeService.getVolumeMetadata(parseInt(volumeId, 10)).pipe(
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
