import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {Router} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgTemplateOutlet} from '@angular/common';
import {SplashContainerComponent} from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {NavService} from "../../../_services/nav.service";
import {AccountService} from "../../../_services/account.service";
import {MemberService} from "../../../_services/member.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";

/**
 * This is exclusively used to register the first user on the server and nothing else
 */
@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  imports: [SplashContainerComponent, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {

  private readonly navService = inject(NavService);
  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly memberService = inject(MemberService);

  registerForm: FormGroup = new FormGroup({
    username: new FormControl('', [Validators.required]),
    email: new FormControl('', []),
    password: new FormControl('', [Validators.required, Validators.maxLength(256),
      Validators.minLength(6), Validators.pattern("^.{6,256}$")]),
  });

  constructor() {

    this.navService.hideNavBar();
    this.navService.hideSideNav();

      this.memberService.adminExists().subscribe(adminExists => {
      if (adminExists) {
        this.router.navigateByUrl('login');
        return;
      }
    });
  }

  submit() {
    const model = this.registerForm.getRawValue();
    this.accountService.register(model).subscribe((user) => {
      this.toastr.success(translate('toasts.account-registration-complete'));
      this.router.navigateByUrl('login');
    });
  }
}
