import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {ApiKeyComponent} from '../api-key/api-key.component';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {AccountService} from "../../_services/account.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {email, form, FormField, FormRoot, required} from "@angular/forms/signals";

interface FormModel {
  email: string;
  password: string;
}

@Component({
  selector: 'app-change-email',
  templateUrl: './change-email.component.html',
  styleUrls: ['./change-email.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, ApiKeyComponent, TranslocoDirective, SettingItemComponent, DefaultValuePipe,
    FormFieldDirective, ValidationErrorsComponent, FormRoot, FormField]
})
export class ChangeEmailComponent {

  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  private readonly formModel = signal<FormModel>({
    email: '',
    password: '',
  });
  formGroup = form(this.formModel, p => {
    required(p.email);
    required(p.password);
    email(p.email);
  });
  errors = signal<string[]>([]);
  isEditMode = signal(false);
  emailLink = signal<string>('');
  emailConfirmed = signal<boolean>(true);
  hasValidEmail = signal<boolean>(true);


  canEdit = computed(() => !this.accountService.hasReadOnlyRole());
  censoredEmail = computed(() => {
    const email = this.accountService.currentUser()?.email;
    if (!email) return null;

    const atIndex = email.indexOf('@');
    const local = atIndex >= 0 ? email.substring(0, atIndex) : email;
    const domain = atIndex >= 0 ? email.substring(atIndex) : '';

    if (local.length <= 2) return local + domain;

    const visible = Math.max(1, Math.ceil(local.length * 0.3));
    return local.substring(0, visible) + '•'.repeat(local.length - visible) + domain;
  })


  constructor() {
    effect(() => {
      const user = this.accountService.currentUser();
      if (!user) return;

      untracked(() => {
        this.formGroup.email().value.set(user!.email);
      });


      this.accountService.isEmailConfirmed().subscribe((confirmed) => {
        this.emailConfirmed.set(confirmed);
      });

      this.accountService.isEmailValid().subscribe(isValid => {
        this.hasValidEmail.set(isValid);
      });
    });
  }

  resetForm() {
    this.formGroup.email().value.set(this.accountService.currentUser()!.email);
    this.errors.set([]);
  }

  saveForm() {
    if (this.accountService.currentUser() === undefined) { return; }

    const model = this.formModel();
    this.errors.set([]);

    this.accountService.updateEmail(model.email, model.password).subscribe(updateEmailResponse => {
      if (updateEmailResponse.invalidEmail) {
        this.toastr.success(translate('toasts.email-sent-to-no-existing', {email: model.email}));
      } else if (updateEmailResponse.emailSent) {
        this.toastr.success(translate('toasts.email-sent-to'));
      } else {
        this.toastr.success(translate('toasts.change-license-email-no-email'));
      }

      this.accountService.refreshAccount().subscribe(() => {
        this.resetForm();
      });
      this.isEditMode.set(false);
    }, err => {
      this.errors.set(err);
    })
  }

  updateEditMode(val: boolean) {
    this.isEditMode.set(val);
  }
}
