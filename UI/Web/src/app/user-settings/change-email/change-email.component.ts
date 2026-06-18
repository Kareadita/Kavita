import {ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, effect, inject} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {AccountService} from 'src/app/_services/account.service';
import {ApiKeyComponent} from '../api-key/api-key.component';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

@Component({
  selector: 'app-change-email',
  templateUrl: './change-email.component.html',
  styleUrls: ['./change-email.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, ReactiveFormsModule, ApiKeyComponent, TranslocoDirective, SettingItemComponent, DefaultValuePipe]
})
export class ChangeEmailComponent {

  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  form: FormGroup = new FormGroup({});
  errors: string[] = [];
  isEditMode: boolean = false;
  emailLink: string = '';
  emailConfirmed: boolean = true;
  hasValidEmail: boolean = true;
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


  protected get email() { return this.form.get('email'); }


  constructor() {
    effect(() => {
      const user = this.accountService.currentUser();
      if (!user) return;

      this.form.addControl('email', new FormControl(user?.email, [Validators.required, Validators.email]));
      this.form.addControl('password', new FormControl('', [Validators.required]));
      this.cdRef.markForCheck();


      this.accountService.isEmailConfirmed().subscribe((confirmed) => {
        this.emailConfirmed = confirmed;
        this.cdRef.markForCheck();
      });

      this.accountService.isEmailValid().subscribe(isValid => {
        this.hasValidEmail = isValid;
        this.cdRef.markForCheck();
      });
    });
  }

  resetForm() {
    this.form.get('email')?.setValue(this.accountService.currentUser()!.email);
    this.errors = [];
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.accountService.currentUser() === undefined) { return; }

    const model = this.form.value;
    this.errors = [];

    this.accountService.updateEmail(model.email, model.password).subscribe(updateEmailResponse => {
      if (updateEmailResponse.invalidEmail) {
        this.toastr.success(translate('toasts.email-sent-to-no-existing', {email: model.email}));
      } else if (updateEmailResponse.emailSent) {
        this.toastr.success(translate('toasts.email-sent-to'));
      } else {
        this.toastr.success(translate('toasts.change-license-email-no-email'));
      }

      this.accountService.refreshAccount().subscribe(user => {
        this.resetForm();
        this.cdRef.markForCheck();
      });
      this.isEditMode = false;
      this.cdRef.markForCheck();
    }, err => {
      this.errors = err;
      this.cdRef.markForCheck();
    })
  }

  updateEditMode(val: boolean) {
    this.isEditMode = val;
    this.cdRef.markForCheck();
  }
}
