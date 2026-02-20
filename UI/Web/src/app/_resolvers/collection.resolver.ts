import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of, switchMap} from "rxjs";
import {UserCollection} from "../_models/collection-tag";
import {CollectionTagService} from "../_services/collection-tag.service";

export const collectionResolver: ResolveFn<UserCollection | UrlTree> = (route, state) => {
  const collectionTagService = inject(CollectionTagService);
  const router = inject(Router);

  const collectionId = route.paramMap.get('collectionId') || route.parent?.paramMap.get('collectionId');

  if (!collectionId || collectionId === '0') {
    return of(router.parseUrl('/collections'));
  }

  return collectionTagService.getCollectionById(parseInt(collectionId, 10)).pipe(
    switchMap(collection => {
      if (collection === null) {
        return of(router.parseUrl('/collections'));
      }
      return of(collection);
    }),
    catchError(() => {
      return of(router.parseUrl('/collections'));
    })
  );
};
