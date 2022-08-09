import { Component, OnInit } from '@angular/core';
import { UntypedFormControl, UntypedFormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { InviteUserResponse } from 'src/app/_models/invite-user-response';
import { Library } from 'src/app/_models/library';
import { AccountService } from 'src/app/_services/account.service';
import { ServerService } from 'src/app/_services/server.service';

@Component({
  selector: 'app-invite-user',
  templateUrl: './invite-user.component.html',
  styleUrls: ['./invite-user.component.scss']
})
export class InviteUserComponent implements OnInit {

  /**
   * Maintains if the backend is sending an email
   */
  isSending: boolean = false;
  inviteForm: UntypedFormGroup = new UntypedFormGroup({});
  selectedRoles: Array<string> = [];
  selectedLibraries: Array<number> = [];
  emailLink: string = '';

  makeLink: (val: string) => string = (val: string) => {return this.emailLink};

  public get email() { return this.inviteForm.get('email'); }

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private serverService: ServerService, 
    private confirmService: ConfirmService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.inviteForm.addControl('email', new UntypedFormControl('', [Validators.required]));
  }

  close() {
    this.modal.close(false);
  }

  invite() {

    this.isSending = true;
    const email = this.inviteForm.get('email')?.value.trim();
    this.accountService.inviteUser({
      email,
      libraries: this.selectedLibraries,
      roles: this.selectedRoles,
    }).subscribe((data: InviteUserResponse) => {
      this.emailLink = data.emailLink;
      this.isSending = false;
      if (data.emailSent) {
        this.toastr.info('Email sent to ' + email);
        this.modal.close(true);
      }
    }, err => {
      this.isSending = false;
    });
  }

  updateRoleSelection(roles: Array<string>) {
    this.selectedRoles = roles;
  }

  updateLibrarySelection(libraries: Array<Library>) {
    this.selectedLibraries = libraries.map(l => l.id);
  }

}
