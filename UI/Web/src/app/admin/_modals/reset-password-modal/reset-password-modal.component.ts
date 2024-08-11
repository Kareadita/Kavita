import {Component, inject, Input} from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/auth/member';
import { AccountService } from 'src/app/_services/account.service';
import { SentenceCasePipe } from '../../../_pipes/sentence-case.pipe';
import { NgIf } from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";

@Component({
    selector: 'app-reset-password-modal',
    templateUrl: './reset-password-modal.component.html',
    styleUrls: ['./reset-password-modal.component.scss'],
    standalone: true,
    imports: [ReactiveFormsModule, NgIf, SentenceCasePipe, TranslocoDirective]
})
export class ResetPasswordModalComponent {

  private readonly toastr = inject(ToastrService);
  private readonly accountService = inject(AccountService);
  public readonly modal = inject(NgbActiveModal);

  @Input({required: true}) member!: Member;

  errorMessage = '';
  resetPasswordForm: FormGroup = new FormGroup({
    password: new FormControl('', [Validators.required]),
  });


  save() {
    this.accountService.resetPassword(this.member.username, this.resetPasswordForm.value.password,'').subscribe(() => {
      this.toastr.success(translate('toasts.password-updated'))
      this.modal.close();
    });
  }

  close() {
    this.modal.close();
  }

}
