import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of, switchMap} from "rxjs";
import {PersonService} from "../_services/person.service";
import {Person} from "../_models/metadata/person";

export const personResolver: ResolveFn<Person | UrlTree> = (route, state) => {
  const personService = inject(PersonService);
  const router = inject(Router);

  const personName = route.parent?.paramMap.get('name') || route.paramMap.get('name');

  if (!personName || personName === '') {
    console.error('Person Name not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return personService.get(personName).pipe(
    switchMap(person => {
      if (person === null) {
        return of(router.parseUrl('/home'));
      }
      return of(person);
    }),
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
