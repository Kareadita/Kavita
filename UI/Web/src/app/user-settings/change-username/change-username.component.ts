import {ChangeDetectionStrategy, Component, effect, inject, signal, untracked} from '@angular/core';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from '@openng/ngx-toastr';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, FormRoot, required} from "@angular/forms/signals";

interface FormModel {
  username: string;
}

@Component({
  selector: 'app-change-username',
  imports: [
    SettingItemComponent,
    TranslocoDirective,
    FormFieldDirective,
    ValidationErrorsComponent,
    FormField,
    FormRoot
  ],
  templateUrl: './change-username.component.html',
  styleUrl: './change-username.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeUsernameComponent {
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  isReadOnly = this.accountService.hasReadOnlyRole;
  username = this.accountService.username;
  private readonly formModel = signal<FormModel>({
    username: ''
  });
  formGroup = form(this.formModel, p => {
    required(p.username);
  });

  isEditMode = signal<boolean>(false);

  constructor() {
    effect(() => {
      untracked(() => {
        this.formGroup.username().value.set(this.accountService.username()!);
      });
    })
  }


  resetPasswordForm() {
    this.formGroup.username().value.set(this.accountService.username()!);
  }

  saveForm() {
    const model = this.formModel();
    this.accountService.changeUsername(model.username).subscribe(() => {
      this.toastr.success(translate('toasts.username-updated'));
      this.resetPasswordForm();
      this.isEditMode.set(false);
    });
  }

  updateEditMode(mode: boolean) {
    this.isEditMode.set(mode);
  }
}
