import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Library } from 'src/app/_models/library';

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

  public get email() { return this.inviteForm.get('email'); }

  constructor(public modal: NgbActiveModal) { }

  ngOnInit(): void {
    this.inviteForm.addControl('email', new FormControl('', [Validators.required]));
  }

  close() {
    this.modal.close(false);
  }

  invite() {
    this.isSending = true;
    setTimeout(() => this.isSending = false, 1000);
    this.modal.close(true);
  }

  updateRoleSelection(roles: Array<string>) {
    // TODO: Hook this up to the invite
  }

  updateLibrarySelection(libraries: Array<Library>) {
    // TODO: Hook this up to the invite
  }

}
