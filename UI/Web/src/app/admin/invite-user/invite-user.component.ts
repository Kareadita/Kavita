import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
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
  accessible: boolean = false;

  public get email() { return this.inviteForm.get('email'); }

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private serverService: ServerService, private confirmService: ConfirmService) { }

  ngOnInit(): void {
    this.inviteForm.addControl('email', new FormControl('', [Validators.required]));

    this.serverService.isServerAccessible().subscribe(async (accessbile) => {
      this.accessible = accessbile;
      if (!this.accessible) {
        await this.confirmService.alert('This server is not accessible. You cannot invite via Email. Please use the password button or correct the issue.');
      }
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
      libraries: [],
      roles: []
    }).subscribe(email => {
      console.log('email', email);
      this.isSending = false;
    });
    this.modal.close(true);
  }

  updateRoleSelection(roles: Array<string>) {
    // TODO: Hook this up to the invite
  }

  updateLibrarySelection(libraries: Array<Library>) {
    // TODO: Hook this up to the invite
  }

}
