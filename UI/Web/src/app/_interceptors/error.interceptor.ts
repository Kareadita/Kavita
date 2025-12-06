import {inject} from '@angular/core';
import {HttpInterceptorFn, HttpRequest} from '@angular/common/http';
import {throwError} from 'rxjs';
import {Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {catchError} from 'rxjs/operators';
import {AccountService} from '../_services/account.service';
import {translate, TranslocoService} from "@jsverse/transloco";
import {AuthGuard} from "../_guards/auth.guard";
import {APP_BASE_HREF} from "@angular/common";

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const toastr = inject(ToastrService);
  const accountService = inject(AccountService);
  const translocoService = inject(TranslocoService);
  const baseURL = inject(APP_BASE_HREF);

  return next(req).pipe(
    catchError(error => {
      if (error === undefined || error === null) {
        return throwError(() => error);
      }

      switch (error.status) {
        case 400:
          handleValidationError(error, toastr);
          break;
        case 401:
          handleAuthError(req, error, accountService, toastr, baseURL);
          break;
        case 404:
          handleNotFound(toastr);
          break;
        case 500:
          handleServerException(error, toastr);
          break;
        case 413:
          handlePayloadTooLargeException(toastr);
          break;
        default:
          const genericError = translate('errors.generic');
          if (toastr.previousToastMessage !== 'Something unexpected went wrong.' &&
            toastr.previousToastMessage !== genericError) {
            toast(genericError, toastr);
          }
          break;
      }
      return throwError(() => error);
    })
  );
};

function handleValidationError(error: any, toastr: ToastrService) {
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
  } else if (error.error && error.error.errors) {
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
        toast('errors.download', toastr, error.status);
        return;
      }
      toast(error.error, toastr,
        translate('errors.error-code', {num: error.status}));
    } else {
      toast(error.statusText === 'OK' ? error.error : error.statusText,
        toastr,
        translate('errors.error-code', {num: error.status}));
    }
  }
}

function handleNotFound(toastr: ToastrService) {
  toast('errors.not-found', toastr);
}

function handlePayloadTooLargeException(toastr: ToastrService) {
  toast('errors.upload-too-large', toastr);
}

function handleServerException(error: any, toastr: ToastrService) {
  const err = error.error;
  if (err.hasOwnProperty('message') && err.message.trim() !== '') {
    if (err.message !== 'User is not authenticated' && error.message !== 'errors.user-not-auth') {
      console.error('500 error: ', error);
    }
    toast(err.message, toastr);
    return;
  }
  if (error.hasOwnProperty('message') && error.message.trim() !== '') {
    if (error.message !== 'User is not authenticated' && error.message !== 'errors.user-not-auth') {
      console.error('500 error: ', error);
    }
    return;
  }

  toast('errors.unknown-crit', toastr);
  console.error('500 error:', error);
}

function handleAuthError(
  req: HttpRequest<unknown>,
  error: any,
  accountService: AccountService,
  toastr: ToastrService,
  baseURL: string
) {
  if (location.href.includes('/registration/confirm-email?token=')) {
    return;
  }

  const path = window.location.pathname;
  if (path !== '/login' && !path.startsWith(baseURL + "registration") && path !== '') {
    localStorage.setItem(AuthGuard.urlKey, path);
  }

  if (error.error && error.error !== 'Unauthorized') {
    toast(translate(error.error), toastr);
  }

  accountService.logout(req.method === 'GET' && req.url.endsWith('/api/account'));
}

function toast(message: string, toastr: ToastrService, title?: string | number) {
  const titleStr = typeof title === 'number' ? title.toString() : title;
  if ((message + '').startsWith('errors.')) {
    toastr.error(translate(message), titleStr);
  } else {
    toastr.error(message, titleStr);
  }
}
