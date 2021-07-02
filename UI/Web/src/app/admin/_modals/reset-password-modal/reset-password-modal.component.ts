import { Component, Input, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/member';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';

@Component({
  selector: 'app-reset-password-modal',
  templateUrl: './reset-password-modal.component.html',
  styleUrls: ['./reset-password-modal.component.scss']
})
export class ResetPasswordModalComponent implements OnInit {

  @Input() member!: Member;
  errorMessage = '';
  resetPasswordForm: FormGroup = new FormGroup({
    password: new FormControl('', [Validators.required]),
  });

  constructor(public modal: NgbActiveModal, private accountService: AccountService) { }

  ngOnInit(): void {
  }

  save() {
    this.accountService.resetPassword(this.member.username, this.resetPasswordForm.value.password).subscribe(() => {
      this.modal.close();
    });
  }

  close() {
    this.modal.close();
  }

}
