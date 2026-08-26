import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from '@openng/ngx-toastr';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";

@Component({
  selector: 'app-change-username',
  imports: [
    SettingItemComponent,
    ReactiveFormsModule,
    TranslocoDirective,
    FormFieldDirective,
    ValidationErrorsComponent
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

  usernameChangeForm: FormGroup = new FormGroup({});
  isEditMode = signal<boolean>(false);

  ngOnInit(): void {
    this.usernameChangeForm.addControl('username', new FormControl(this.accountService.username(), [Validators.required]));
  }


  resetPasswordForm() {
    this.usernameChangeForm.get('username')?.setValue(this.accountService.username());
  }

  saveForm() {
    const model = this.usernameChangeForm.value;
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
