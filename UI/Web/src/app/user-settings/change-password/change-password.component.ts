import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { map, Observable, of, shareReplay, Subject, take, takeUntil } from 'rxjs';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-change-password',
  templateUrl: './change-password.component.html',
  styleUrls: ['./change-password.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangePasswordComponent implements OnInit, OnDestroy {

  passwordChangeForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;
  hasChangePasswordAbility: Observable<boolean> = of(false);
  observableHandles: Array<any> = [];
  passwordsMatch = false;
  resetPasswordErrors: string[] = [];
   isViewMode: boolean = true;

  public get password() { return this.passwordChangeForm.get('password'); }
  public get confirmPassword() { return this.passwordChangeForm.get('confirmPassword'); }

  private onDestroy = new Subject<void>();

  constructor(private accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {

    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), shareReplay(), take(1)).subscribe(user => {
      this.user = user;
      this.cdRef.markForCheck();
    });

    this.hasChangePasswordAbility = this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), shareReplay(), map(user => {
      return user !== undefined && (this.accountService.hasAdminRole(user) || this.accountService.hasChangePasswordRole(user));
    }));
    this.cdRef.markForCheck();

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
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  resetPasswordForm() {
    this.passwordChangeForm.get('password')?.setValue('');
    this.passwordChangeForm.get('confirmPassword')?.setValue('');
    this.passwordChangeForm.get('oldPassword')?.setValue('');
    this.resetPasswordErrors = [];
    this.cdRef.markForCheck();
  }

  savePasswordForm() {
    if (this.user === undefined) { return; }

    const model = this.passwordChangeForm.value;
    this.resetPasswordErrors = [];
    this.observableHandles.push(this.accountService.resetPassword(this.user?.username, model.confirmPassword, model.oldPassword).subscribe(() => {
      this.toastr.success('Password has been updated');
      this.resetPasswordForm();
      this.isViewMode = true;
    }, err => {
      this.resetPasswordErrors = err;
    }));
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetPasswordForm();
  }
}
