import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { catchError, take } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {

  constructor(private router: Router, private toastr: ToastrService, private accountService: AccountService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError(error => {
        if (error && error.status !== 200) {
          switch (error.status) {
            case 400:
              // IF type of array, this comes from signin handler
              if (Array.isArray(error.error)) {
                const modalStateErrors: any[] = [];
                if (error.error.length > 0 && error.error[0].hasOwnProperty('message')) {
                  error.error.forEach((issue: {status: string, details: string, message: string}) => {
                    modalStateErrors.push(issue.details);
                  });
                } else {
                  error.error.forEach((issue: {code: string, description: string}) => {
                    modalStateErrors.push(issue.description);
                  });
                }
                
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
                console.error('error:', error);
                if (error.statusText === 'Bad Request') {
                  this.toastr.error(error.error, error.status);
                } else {
                  this.toastr.error(error.statusText === 'OK' ? error.error : error.statusText, error.status);
                }
              }
              break;
            case 401:
              // if statement is due to http/2 spec issue: https://github.com/angular/angular/issues/23334
              this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
                if (user) {
                  this.toastr.error(error.statusText === 'OK' ? 'Unauthorized' : error.statusText, error.status);
                }
                this.accountService.logout();
                this.router.navigateByUrl('/login');
              });
              break;
            case 404:
              console.error('error:', error);
              this.toastr.error('That url does not exist.');
              break;
            case 500:
              console.error('error:', error);
              const err = error.error;
              if (err.hasOwnProperty('message') && err['message'].trim() != '') {
                this.toastr.error(err.message);
              } else {
                this.toastr.error('There was an unknown critical error.');
              }
              break;
            default:
              if (this.toastr.previousToastMessage !== 'Something unexpected went wrong.') {
                this.toastr.error('Something unexpected went wrong.');
              }

              if (!localStorage.getItem('kavita--no-connection-url')) {
                localStorage.setItem('kavita--no-connection-url', this.router.url);
              }

              if (this.router.url !== '/no-connection') {
                this.router.navigateByUrl('/no-connection');
              }
              break;
          }
        }
        return throwError(error);
      })
    );
  }
}
