import {ChangeDetectionStrategy, Component, inject, OnDestroy, signal} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgTemplateOutlet} from '@angular/common';
import {SplashContainerComponent} from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {ThemeService} from "../../../_services/theme.service";
import {NavService} from "../../../_services/nav.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {email, form, FormField, maxLength, minLength, readonly, required} from "@angular/forms/signals";
import {catchError, EMPTY, tap} from "rxjs";

@Component({
    selector: 'app-confirm-email',
    templateUrl: './confirm-email.component.html',
    styleUrls: ['./confirm-email.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, NgbTooltip, NgTemplateOutlet, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField]
})
export class ConfirmEmailComponent implements OnDestroy {

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private themeService = inject(ThemeService);
  private navService = inject(NavService);


  formModel = signal({
    token: '',
    email: '',
    username: '',
    password: ''
  });
  formGroup = form(this.formModel, path => {
    required(path);
    email(path.email);
    minLength(path.password, 6);
    maxLength(path.password, 256);
    readonly(path.email);
  });

  /**
   * Validation errors from API
   */
  errors = signal<string[]>([]);


  constructor() {
      this.navService.hideSideNav();
      this.themeService.setTheme(this.themeService.defaultTheme);
      const token = this.route.snapshot.queryParamMap.get('token');
      const emailQuery = this.route.snapshot.queryParamMap.get('email');
      if (this.isNullOrEmpty(token) || this.isNullOrEmpty(emailQuery)) {
        // This is not a valid url, redirect to login
        this.toastr.error(translate('errors.invalid-confirmation-url'));
        this.router.navigateByUrl('login');
        return;
      }
      this.formModel.set({
        token: token!,
        email: emailQuery || '',
        password: '',
        username: '',
      });
  }

  ngOnDestroy() {
    if (this.accountService.isLoggedIn()) {
      this.navService.showSideNav();
    }
  }

  isNullOrEmpty(v: string | null | undefined) {
    return v == undefined || v === '' || v === null;
  }

  submit() {
    this.accountService.confirmEmail(this.formModel()).pipe(
      catchError(err => {
        console.error('Error from Confirming Email: ', err);
        this.errors.set([...err]);
        return EMPTY;
      }),
      tap(() => {
        this.toastr.success(translate('toasts.account-registration-complete'));
        this.router.navigateByUrl('login');
      })
    ).subscribe();
  }

}
