import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../_services/account.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, FormRoot, required} from "@angular/forms/signals";
import {mustMatchValidator} from "../../_validators/must-match.validator";

interface FormModel {
  password: string;
  oldPassword: string;
  confirmPassword: string;
}


@Component({
  selector: 'app-change-password',
  templateUrl: './change-password.component.html',
  styleUrls: ['./change-password.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, SettingItemComponent, FormFieldDirective, ValidationErrorsComponent, FormRoot, FormField]
})
export class ChangePasswordComponent {

  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  private readonly formModel = signal<FormModel>({
    password: '',
    oldPassword: '',
    confirmPassword: ''
  });
  formGroup = form(this.formModel, p => {
    required(p.password);
    required(p.confirmPassword);
    required(p.confirmPassword);

    mustMatchValidator(p.oldPassword, p.confirmPassword);
  });

  hasChangePasswordAbility = computed(() => {
    const readOnly = this.accountService.hasReadOnlyRole();
    const isAdmin = this.accountService.hasAdminRole();
    const changePassword = this.accountService.hasChangePasswordRole();
    return !readOnly && (isAdmin || changePassword);
  });
  resetPasswordErrors = signal<string[]>([]);
  isEditMode = signal(false);

  resetPasswordForm() {
    this.formGroup.password().value.set('');
    this.formGroup.confirmPassword().value.set('');
    this.formGroup.oldPassword().value.set('');
    this.resetPasswordErrors.set([]);
  }

  savePasswordForm() {
    const model = this.formModel();
    this.resetPasswordErrors.set([]);
    this.accountService.resetPassword(this.accountService.username()!, model.confirmPassword, model.oldPassword).subscribe(() => {
      this.toastr.success(translate('toasts.password-updated'));
      this.resetPasswordForm();
      this.isEditMode.set(false);
    }, err => {
      this.resetPasswordErrors.set(err);
    })
  }

  updateEditMode(mode: boolean) {
    this.isEditMode.set(mode);
  }

}
