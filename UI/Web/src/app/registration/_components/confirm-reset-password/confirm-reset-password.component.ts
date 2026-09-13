import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {NgTemplateOutlet} from '@angular/common';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {SplashContainerComponent} from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {NavService} from "../../../_services/nav.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {email, form, FormField, maxLength, minLength, required} from "@angular/forms/signals";

@Component({
    selector: 'app-confirm-reset-password',
    templateUrl: './confirm-reset-password.component.html',
    styleUrls: ['./confirm-reset-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, NgbTooltip, NgTemplateOutlet, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField]
})
export class ConfirmResetPasswordComponent {

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private navService = inject(NavService);

  formModel = signal({
    email: '',
    password: '',
    token: '',
  });
  formGroup = form(this.formModel, path => {
    required(path.email);
    email(path.email);
    required(path.password);
    minLength(path.password, 6);
    maxLength(path.password, 256);
    required(path.token);
  });

  constructor() {

      this.navService.showNavBar();
      this.navService.hideSideNav();


    const token = this.route.snapshot.queryParamMap.get('token');
    const queryEmail = this.route.snapshot.queryParamMap.get('email');
    if (token == undefined || token === '' || token === null) {
      // This is not a valid url, redirect to login
      this.toastr.error(translate('errors.invalid-password-reset-url'));
      this.router.navigateByUrl('login');
      return;
    }

    this.formModel.set({
      token: token,
      email: queryEmail ?? '',
      password: ''
    });
  }


  submit() {
    this.accountService.confirmResetPasswordEmail(this.formModel()).subscribe(() => {
      this.toastr.success(translate('toasts.password-reset'));
      this.router.navigateByUrl('login');
    });
  }
}
