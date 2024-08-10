import { ChangeDetectionStrategy, ChangeDetectorRef, Component } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { NgTemplateOutlet, NgIf } from '@angular/common';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { SplashContainerComponent } from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-confirm-reset-password',
    templateUrl: './confirm-reset-password.component.html',
    styleUrls: ['./confirm-reset-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [SplashContainerComponent, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, NgIf, TranslocoDirective]
})
export class ConfirmResetPasswordComponent {

  token: string = '';
  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required, Validators.maxLength(256), Validators.minLength(6)]),
  });

  constructor(private route: ActivatedRoute, private router: Router,
    private accountService: AccountService, private toastr: ToastrService,
    private readonly cdRef: ChangeDetectorRef, private navService: NavService) {

      this.navService.showNavBar();
      this.navService.hideSideNav();


    const token = this.route.snapshot.queryParamMap.get('token');
    const email = this.route.snapshot.queryParamMap.get('email');
    if (token == undefined || token === '' || token === null) {
      // This is not a valid url, redirect to login
      this.toastr.error(translate('errors.invalid-password-reset-url'));
      this.router.navigateByUrl('login');
      return;
    }

    this.token = token;
    this.registerForm.get('email')?.setValue(email);
    this.cdRef.markForCheck();
  }


  submit() {
    const model = this.registerForm.getRawValue();
    model.token = this.token;
    this.accountService.confirmResetPasswordEmail(model).subscribe((response: string) => {
      this.toastr.success(translate('toasts.password-reset'));
      this.router.navigateByUrl('login');
    }, err => {
      console.error(err, 'There was an error trying to confirm reset password');
    });
  }
}
