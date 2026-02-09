import {ResolveFn} from "@angular/router";
import {inject} from "@angular/core";
import {Library} from "../_models/library/library";
import {LibraryService} from "../_services/library.service";
import {catchError, EMPTY} from "rxjs";

export const libraryResolver: ResolveFn<Library> = (route, state) => {
  const libraryService = inject(LibraryService);

  const libId = route.paramMap.get('libraryId') || route.parent?.paramMap.get('libraryId');

  if (!libId) {
    console.error('Library ID not found in route params');
    return EMPTY; // Or redirect
  }

  return libraryService.getLibrary(parseInt(libId, 10)).pipe(
    catchError(() => {
      // Handle fetch errors so the navigation doesn't just "freeze"
      return EMPTY;
    })
  );
};
