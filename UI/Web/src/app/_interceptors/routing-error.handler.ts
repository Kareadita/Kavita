import {NavigationError, Router} from "@angular/router";
import {inject} from "@angular/core";


export function routingErrorHandler(err: NavigationError) {
  const router = inject(Router);
  console.error(err)

  router.navigate(['/home']);
}
