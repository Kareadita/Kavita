import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ToastrService} from '@openng/ngx-toastr';
import {AccountService} from "../../../_services/account.service";
import {Member} from "../../../_models/auth/member";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";

@Component({
  selector: 'app-reset-password-modal',
  templateUrl: './reset-password-modal.component.html',
  styleUrls: ['./reset-password-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, SentenceCasePipe, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent]
})
export class ResetPasswordModalComponent {

  private readonly toastr = inject(ToastrService);
  private readonly accountService = inject(AccountService);
  protected readonly modal = inject(NgbActiveModal);

  member = input.required<Member>();

  resetPasswordForm: FormGroup = new FormGroup({
    password: new FormControl('', [Validators.required, Validators.minLength(4)]),
  });


  save() {
    this.accountService.resetPassword(this.member().username, this.resetPasswordForm.value.password,'').subscribe(() => {
      this.toastr.success(translate('toasts.password-updated'))
      this.modal.close();
    });
  }

  close() {
    this.modal.dismiss();
  }

}
