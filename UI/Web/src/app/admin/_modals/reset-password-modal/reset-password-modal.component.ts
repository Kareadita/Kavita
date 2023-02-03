import { Component, Input } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/auth/member';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-reset-password-modal',
  templateUrl: './reset-password-modal.component.html',
  styleUrls: ['./reset-password-modal.component.scss']
})
export class ResetPasswordModalComponent {

  @Input() member!: Member;
  errorMessage = '';
  resetPasswordForm: FormGroup = new FormGroup({
    password: new FormControl('', [Validators.required]),
  });

  constructor(public modal: NgbActiveModal, private accountService: AccountService) { }

  save() {
    this.accountService.resetPassword(this.member.username, this.resetPasswordForm.value.password,'').subscribe(() => {
      this.modal.close();
    });
  }

  close() {
    this.modal.close();
  }

}
