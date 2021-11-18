import { Injectable, OnDestroy } from '@angular/core';
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
import { environment } from 'src/environments/environment';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {

  public urlKey: string = 'kavita--no-connection-url';
  constructor(private router: Router, private toastr: ToastrService, private accountService: AccountService) {}


  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError(error => {
        if (error === undefined || error === null) {
          return throwError(error);
        }

        switch (error.status) {
          case 400:
            this.handleValidationError(error);
            break;
          case 401:
            this.handleAuthError(error);
            break;
          case 404:
            this.handleNotFound(error);
            break;
          case 500:
            this.handleServerException(error);
            break;
          default:
            // Don't throw multiple Something undexpected went wrong
            if (this.toastr.previousToastMessage !== 'Something unexpected went wrong.') {
              this.toastr.error('Something unexpected went wrong.');
            }

            // If we are not on no-connection, redirect there and save current url so when we refersh, we redirect back there
            // if (this.router.url !== '/no-connection') {
            //   localStorage.setItem(this.urlKey, this.router.url);
            //   this.router.navigateByUrl('/no-connection');
            // }
            break;
        }
        return throwError(error);
      })
    );
  }

  private handleValidationError(error: any) {
    // This 400 can also be a bad request 
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
  }

  private handleNotFound(error: any) {
    this.toastr.error('That url does not exist.'); 
  }

  private handleServerException(error: any) {
    const err = error.error;
    if (err.hasOwnProperty('message') && err.message.trim() !== '') {
      if (err.message != 'User is not authenticated') {
        console.log('500 error: ', error);
      }
      this.toastr.error(err.message);
    } else {
      this.toastr.error('There was an unknown critical error.');
      console.error('500 error:', error);
    }
  }

  private handleAuthError(error: any) {
    // NOTE: Signin has error.error or error.statusText available. 
    // if statement is due to http/2 spec issue: https://github.com/angular/angular/issues/23334
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.accountService.logout();
    });
  }
}
