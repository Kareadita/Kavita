import { ChangeDetectionStrategy, Component } from '@angular/core';
import { UntypedFormGroup, UntypedFormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResetPasswordComponent {

  registerForm: UntypedFormGroup = new UntypedFormGroup({
    email: new UntypedFormControl('', [Validators.required, Validators.email]),
  });

  constructor(private router: Router, private accountService: AccountService, 
    private toastr: ToastrService) {}

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
