import {ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ThemeService } from 'src/app/_services/theme.service';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { NgTemplateOutlet } from '@angular/common';
import { SplashContainerComponent } from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {take} from "rxjs/operators";

@Component({
    selector: 'app-confirm-email',
    templateUrl: './confirm-email.component.html',
    styleUrls: ['./confirm-email.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SplashContainerComponent, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, TranslocoDirective]
})
export class ConfirmEmailComponent implements OnDestroy {
  /**
   * Email token used for validating
   */
  token: string = '';

  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.required]),
    username: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, Validators.maxLength(256), Validators.minLength(6), Validators.pattern("^.{6,256}$")]),
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
      if (this.isNullOrEmpty(token) || this.isNullOrEmpty(email)) {
        // This is not a valid url, redirect to login
        this.toastr.error(translate('errors.invalid-confirmation-url'));
        this.router.navigateByUrl('login');
        return;
      }
      this.token = token!;
      this.registerForm.get('email')?.setValue(email || '');
      this.cdRef.markForCheck();
  }

  ngOnDestroy() {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.navService.showSideNav();
      }
    });
  }

  isNullOrEmpty(v: string | null | undefined) {
    return v == undefined || v === '' || v === null;
  }

  submit() {
    const model = this.registerForm.getRawValue();
    model.token = this.token;
    this.accountService.confirmEmail(model).subscribe((user) => {
      this.toastr.success(translate('toasts.account-registration-complete'));
      this.router.navigateByUrl('login');
    }, err => {
      console.error('Error from Confirming Email: ', err);
      this.errors = err;
      this.cdRef.markForCheck();
    });
  }

}
