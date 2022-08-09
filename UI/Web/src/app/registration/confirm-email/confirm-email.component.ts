import { ChangeDetectionStrategy, ChangeDetectorRef, Component } from '@angular/core';
import { UntypedFormControl, UntypedFormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ThemeService } from 'src/app/_services/theme.service';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';

@Component({
  selector: 'app-confirm-email',
  templateUrl: './confirm-email.component.html',
  styleUrls: ['./confirm-email.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmEmailComponent {
  /**
   * Email token used for validating
   */
  token: string = '';

  registerForm: UntypedFormGroup = new UntypedFormGroup({
    email: new UntypedFormControl('', [Validators.required, Validators.email]),
    username: new UntypedFormControl('', [Validators.required]),
    password: new UntypedFormControl('', [Validators.required, Validators.maxLength(32), Validators.minLength(6)]),
  });

  /**
   * Validation errors from API
   */
  errors: Array<string> = [];


  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService, 
    private toastr: ToastrService, private themeService: ThemeService, private navService: NavService, 
    private readonly cdRef: ChangeDetectorRef) {
      this.navService.hideSideNav();
      this.themeService.setTheme(this.themeService.defaultTheme);
      const token = this.route.snapshot.queryParamMap.get('token');
      const email = this.route.snapshot.queryParamMap.get('email');
      this.cdRef.markForCheck();
      if (token == undefined || token === '' || token === null) {
        // This is not a valid url, redirect to login
        this.toastr.error('Invalid confirmation email');
        this.router.navigateByUrl('login');
        return;
      }
      this.token = token;
      this.registerForm.get('email')?.setValue(email || '');
      this.cdRef.markForCheck();
  }

  submit() {
    let model = this.registerForm.getRawValue();
    model.token = this.token;
    this.accountService.confirmEmail(model).subscribe((user) => {
      this.toastr.success('Account registration complete');
      this.router.navigateByUrl('login');
    }, err => {
      console.log('error: ', err);
      this.errors = err;
      this.cdRef.markForCheck();
    });
  }

}
