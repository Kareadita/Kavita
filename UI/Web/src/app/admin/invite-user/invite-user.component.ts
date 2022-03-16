import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from 'src/app/shared/confirm.service';
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
  inviteForm: FormGroup = new FormGroup({});
  /**
   * If a user would be able to load this server up externally
   */
  accessible: boolean = true;
  checkedAccessibility: boolean = false;
  selectedRoles: Array<string> = [];
  selectedLibraries: Array<number> = [];
  emailLink: string = '';

  public get email() { return this.inviteForm.get('email'); }

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private serverService: ServerService, 
    private confirmService: ConfirmService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.inviteForm.addControl('email', new FormControl('', [Validators.required]));

    this.serverService.isServerAccessible().subscribe(async (accessibile) => {
      if (!accessibile) {
        await this.confirmService.alert('This server is not accessible outside the network. You cannot invite via Email. You wil be given a link to finish registration with instead.');
        this.accessible = accessibile;
      }
      this.checkedAccessibility = true;
    });
  }

  close() {
    this.modal.close(false);
  }

  invite() {

    this.isSending = true;
    const email = this.inviteForm.get('email')?.value;
    this.accountService.inviteUser({
      email,
      libraries: this.selectedLibraries,
      roles: this.selectedRoles,
      sendEmail: this.accessible
    }).subscribe(emailLink => {
      this.emailLink = emailLink;
      this.isSending = false;
      if (this.accessible) {
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
