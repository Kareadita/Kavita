import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {Router} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {SplashContainerComponent} from '../splash-container/splash-container.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {NavService} from "../../../_services/nav.service";
import {AccountService} from "../../../_services/account.service";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

@Component({
    selector: 'app-reset-password',
    templateUrl: './reset-password.component.html',
    styleUrls: ['./reset-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, ReactiveFormsModule, TranslocoDirective, ValidationErrorsComponent, FormFieldDirective]
})
export class ResetPasswordComponent {

  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly navService = inject(NavService);

  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
  });

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
  }

  submit() {
    const model = this.registerForm.get('email')?.value;
    this.accountService.requestResetPasswordEmail(model).subscribe((resp: string) => {
      this.toastr.info(resp);
      this.router.navigateByUrl('login');
    }, err => {
      this.toastr.error(err.error);
    });
  }

}
