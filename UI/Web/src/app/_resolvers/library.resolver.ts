import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {Library} from "../_models/library/library";
import {LibraryService} from "../_services/library.service";
import {catchError, of} from "rxjs";

export const libraryResolver: ResolveFn<Library | UrlTree> = (route, state) => {
  const libraryService = inject(LibraryService);
  const router = inject(Router);

  const libId = route.paramMap.get('libraryId') || route.parent?.paramMap.get('libraryId');

  if (!libId) {
    console.error('Library ID not found in route params');
    return of(router.parseUrl('/home'));
  }

  return libraryService.getLibrary(parseInt(libId, 10)).pipe(
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
