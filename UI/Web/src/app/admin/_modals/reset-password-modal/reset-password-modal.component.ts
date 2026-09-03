import {ChangeDetectionStrategy, Component, inject, input, signal} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ToastrService} from '@openng/ngx-toastr';
import {AccountService} from "../../../_services/account.service";
import {Member} from "../../../_models/auth/member";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, minLength, required} from "@angular/forms/signals";

interface ResetPasswordForm {
  password: string;
}
@Component({
  selector: 'app-reset-password-modal',
  templateUrl: './reset-password-modal.component.html',
  styleUrls: ['./reset-password-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, SentenceCasePipe, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField]
})
export class ResetPasswordModalComponent {

  private readonly toastr = inject(ToastrService);
  private readonly accountService = inject(AccountService);
  protected readonly modal = inject(NgbActiveModal);

  member = input.required<Member>();

  private readonly resetPasswordModel = signal<ResetPasswordForm>({
    password: ''
  });
  resetPasswordForm = form(this.resetPasswordModel, (schemaPath) => {
    required(schemaPath.password);
    minLength(schemaPath.password, 4);
  });


  save() {
    this.accountService.resetPassword(this.member().username, this.resetPasswordForm.password().value(),'').subscribe(() => {
      this.toastr.success(translate('toasts.password-updated'))
      this.modal.close();
    });
  }

  close() {
    this.modal.dismiss();
  }

}
