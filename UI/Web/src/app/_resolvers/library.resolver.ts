import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {Library} from "../_models/library/library";
import {LibraryService} from "../_services/library.service";
import {catchError, of} from "rxjs";

/**
 * Get Library is an admin-restricted API, in the case the user is not an Admin, this will return a Library with just the id/type/name
 * @param route
 * @param state
 */
export const libraryResolver: ResolveFn<Library | UrlTree> = (route, state) => {
  const libraryService = inject(LibraryService);
  const router = inject(Router);

  const libId = route.parent?.paramMap.get('libraryId') || route.paramMap.get('libraryId');

  if (!libId || libId === '0') {
    console.error('Library ID not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return libraryService.getLibrary(parseInt(libId, 10)).pipe(
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
