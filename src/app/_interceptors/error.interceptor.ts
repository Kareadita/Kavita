import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { NavigationExtras, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { catchError } from 'rxjs/operators';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {

  constructor(private router: Router, private toastr: ToastrService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError(error => {
        if (error) {
          console.error('error:', error);
          switch (error.status) {
            case 400:
              // IF type of array, this comes from signin handler
              if (Array.isArray(error.error)) {
                const modalStateErrors: any[] = [];
                error.error.forEach((issue: {code: string, description: string}) => {
                  modalStateErrors.push(issue.description);
                });
                throw modalStateErrors.flat();
              } else if (error.error.errors) {
                // Validation error
                const modalStateErrors = [];
                for (const key in error.error.errors) {
                  if (error.error.errors[key]) {
                    modalStateErrors.push(error.error.errors[key]);
                  }
                }
                throw modalStateErrors.flat();
              } else {
                this.toastr.error(error.statusText === 'OK' ? error.error : error.statusText, error.status);
              }
              break;
            case 401:
              // if statement is due to http/2 spec issue: https://github.com/angular/angular/issues/23334
              this.toastr.error(error.statusText === 'OK' ? 'Unauthorized' : error.statusText, error.status);
              this.router.navigateByUrl('/login');
              break;
            case 404:
              this.router.navigateByUrl('/not-found');
              break;
            case 500:
              const navigationExtras: NavigationExtras = {state: {error: error.error}};
              this.router.navigateByUrl('/server-error', navigationExtras);
              break;
            default:
              this.toastr.error('Something unexpected went wrong.');
              console.log(error);
              break;
          }
        }
        return throwError(error);
      })
    );
  }
}
