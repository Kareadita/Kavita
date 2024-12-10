import {Injectable} from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { catchError } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';
import {translate, TranslocoService} from "@jsverse/transloco";

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(private router: Router, private toastr: ToastrService,
              private accountService: AccountService,
              private translocoService: TranslocoService) {}


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
            // Don't throw multiple Something unexpected went wrong
            const genericError = translate('errors.generic');
            if (this.toastr.previousToastMessage !== 'Something unexpected went wrong.' && this.toastr.previousToastMessage !== genericError) {
              this.toast(genericError);
            }
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
        if (error.error[0].details === null) {
          error.error.forEach((issue: {status: string, details: string, message: string}) => {
            modalStateErrors.push(issue.message);
          });
        } else {
          error.error.forEach((issue: {status: string, details: string, message: string}) => {
            modalStateErrors.push(issue.details);
          });
        }
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
        if (error.error instanceof Blob) {
          this.toast('errors.download', error.status);
          return;
        }
        this.toast(error.error, this.translocoService.translate('errors.error-code', {num: error.status}));
      } else {
        this.toast(error.statusText === 'OK' ? error.error : error.statusText, this.translocoService.translate('errors.error-code', {num: error.status}));
      }
    }
  }

  private handleNotFound(error: any) {
    this.toast('errors.not-found');
  }

  private handleServerException(error: any) {
    const err = error.error;
    if (err.hasOwnProperty('message') && err.message.trim() !== '') {
      if (err.message != 'User is not authenticated' && error.message !== 'errors.user-not-auth') {
        console.error('500 error: ', error);
      }
      this.toast(err.message);
      return;
    }
    if (error.hasOwnProperty('message') && error.message.trim() !== '') {
      if (error.message !== 'User is not authenticated' && error.message !== 'errors.user-not-auth') {
        console.error('500 error: ', error);
      }
      return;
    }

    this.toast('errors.unknown-crit');
    console.error('500 error:', error);
  }

  private handleAuthError(error: any) {
    // Special hack for register url, to not care about auth
    if (location.href.includes('/registration/confirm-email?token=')) {
      return;
    }
    // NOTE: Signin has error.error or error.statusText available.
    // if statement is due to http/2 spec issue: https://github.com/angular/angular/issues/23334
    this.accountService.logout();
  }

  // Assume the title is already translated
  private toast(message: string, title?: string) {
    if (message.startsWith('errors.')) {
      this.toastr.error(this.translocoService.translate(message), title);
    } else {
      this.toastr.error(message, title);
    }
  }

}
