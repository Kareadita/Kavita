import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {AccountService} from 'src/app/_services/account.service';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-change-password',
    templateUrl: './change-password.component.html',
    styleUrls: ['./change-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslocoDirective, SettingItemComponent]
})
export class ChangePasswordComponent implements OnInit, OnDestroy {

  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);

  passwordChangeForm: FormGroup = new FormGroup({});
  hasChangePasswordAbility = computed(() => {
    const readOnly = this.accountService.hasReadOnlyRole();
    const isAdmin = this.accountService.hasAdminRole();
    const changePassword = this.accountService.hasChangePasswordRole();
    return !readOnly && (isAdmin || changePassword);
  });
  observableHandles: Array<any> = [];
  passwordsMatch = false;
  resetPasswordErrors: string[] = [];
  isEditMode: boolean = false;



  public get password() { return this.passwordChangeForm.get('password'); }
  public get confirmPassword() { return this.passwordChangeForm.get('confirmPassword'); }

  ngOnInit(): void {
    this.passwordChangeForm.addControl('password', new FormControl('', [Validators.required]));
    this.passwordChangeForm.addControl('confirmPassword', new FormControl('', [Validators.required]));
    this.passwordChangeForm.addControl('oldPassword', new FormControl('', [Validators.required]));

    this.observableHandles.push(this.passwordChangeForm.valueChanges.subscribe(() => {
      const values = this.passwordChangeForm.value;
      this.passwordsMatch = values.password === values.confirmPassword;
      this.cdRef.markForCheck();
    }));
  }

  ngOnDestroy() {
    this.observableHandles.forEach(o => o.unsubscribe());
  }

  resetPasswordForm() {
    this.passwordChangeForm.get('password')?.setValue('');
    this.passwordChangeForm.get('confirmPassword')?.setValue('');
    this.passwordChangeForm.get('oldPassword')?.setValue('');
    this.resetPasswordErrors = [];
    this.cdRef.markForCheck();
  }

  savePasswordForm() {
    const model = this.passwordChangeForm.value;
    this.resetPasswordErrors = [];
    this.observableHandles.push(this.accountService.resetPassword(this.accountService.username()!, model.confirmPassword, model.oldPassword).subscribe(() => {
      this.toastr.success(translate('toasts.password-updated'));
      this.resetPasswordForm();
      this.isEditMode = false;
      this.cdRef.markForCheck();
    }, err => {
      this.resetPasswordErrors = err;
      this.cdRef.markForCheck();
    }));
  }

  updateEditMode(mode: boolean) {
    this.isEditMode = mode;
    this.cdRef.markForCheck();
  }
}
