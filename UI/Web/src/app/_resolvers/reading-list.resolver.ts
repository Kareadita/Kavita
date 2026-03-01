import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of, switchMap} from "rxjs";
import {ReadingList} from "../_models/reading-list";
import {ReadingListService} from "../_services/reading-list.service";

export const readingListResolver: ResolveFn<ReadingList | UrlTree> = (route, state) => {
  const readingListService = inject(ReadingListService);
  const router = inject(Router);

  const readingListId = route.paramMap.get('readingListId') || route.parent?.paramMap.get('readingListId');

  if (!readingListId || readingListId === '0') {
    console.error('Reading List ID not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return readingListService.getReadingList(parseInt(readingListId, 10)).pipe(
    switchMap(readingList => {
      if (readingList === null) {
        return of(router.parseUrl('/home'));
      }
      return of(readingList);
    }),
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
