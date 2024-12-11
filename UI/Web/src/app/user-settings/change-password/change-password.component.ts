import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { map, Observable, of, shareReplay } from 'rxjs';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import { AsyncPipe } from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-change-password',
    templateUrl: './change-password.component.html',
    styleUrls: ['./change-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbCollapse, ReactiveFormsModule, AsyncPipe, TranslocoDirective, SettingTitleComponent, SettingItemComponent]
})
export class ChangePasswordComponent implements OnInit, OnDestroy {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);

  passwordChangeForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;
  hasChangePasswordAbility: Observable<boolean> = of(false);
  observableHandles: Array<any> = [];
  passwordsMatch = false;
  resetPasswordErrors: string[] = [];
  isViewMode: boolean = true;
  canEdit: boolean = false;


  public get password() { return this.passwordChangeForm.get('password'); }
  public get confirmPassword() { return this.passwordChangeForm.get('confirmPassword'); }

  ngOnInit(): void {

    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay()).subscribe(user => {
      this.user = user;
      this.canEdit = !this.accountService.hasReadOnlyRole(user!);
      this.cdRef.markForCheck();
    });

    this.hasChangePasswordAbility = this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), map(user => {
      return user !== undefined && !this.accountService.hasReadOnlyRole(user) && (this.accountService.hasAdminRole(user) || this.accountService.hasChangePasswordRole(user));
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
      this.toastr.success(translate('toasts.password-updated'));
      this.resetPasswordForm();
      this.isViewMode = true;
      this.cdRef.markForCheck();
    }, err => {
      this.resetPasswordErrors = err;
      this.cdRef.markForCheck();
    }));
  }

  updateEditMode(mode: boolean) {
    this.isViewMode = !mode;
    this.cdRef.markForCheck();
  }
}
