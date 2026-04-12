import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-change-username',
  imports: [
    SettingItemComponent,
    ReactiveFormsModule,
    TranslocoDirective
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
  observableHandles: Array<any> = [];
  errors = signal<string[]>([]);
  isEditMode = signal<boolean>(false);

  ngOnInit(): void {
    this.usernameChangeForm.addControl('username', new FormControl(this.accountService.username(), [Validators.required]));
  }

  ngOnDestroy() {
    this.observableHandles.forEach(o => o.unsubscribe());
  }

  resetPasswordForm() {
    this.usernameChangeForm.get('username')?.setValue(this.accountService.username());
    this.errors.set([]);
  }

  saveForm() {
    const model = this.usernameChangeForm.value;
    this.errors.set([]);
    this.accountService.changeUsername(model.username).subscribe(() => {
      this.toastr.success(translate('toasts.username-updated'));
      this.resetPasswordForm();
      this.isEditMode.set(false);
    }, err => {
      this.errors.set(err);
    });
  }

  updateEditMode(mode: boolean) {
    this.isEditMode.set(mode);
  }
}
