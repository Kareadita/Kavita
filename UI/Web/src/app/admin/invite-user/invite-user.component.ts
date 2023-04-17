import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { InviteUserResponse } from 'src/app/_models/auth/invite-user-response';
import { Library } from 'src/app/_models/library';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { AccountService } from 'src/app/_services/account.service';

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
  selectedRoles: Array<string> = [];
  selectedLibraries: Array<number> = [];
  selectedRestriction: AgeRestriction = {ageRating: AgeRating.NotApplicable, includeUnknowns: false};
  emailLink: string = '';

  makeLink: (val: string) => string = (val: string) => {return this.emailLink};

  public get hasAdminRoleSelected() { return this.selectedRoles.includes('Admin'); };

  public get email() { return this.inviteForm.get('email'); }

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.inviteForm.addControl('email', new FormControl('', [Validators.required]));
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
      ageRestriction: this.selectedRestriction
    }).subscribe((data: InviteUserResponse) => {
      this.emailLink = data.emailLink;
      this.isSending = false;
      if (data.emailSent) {
        this.toastr.info('Email sent to ' + email);
        this.modal.close(true);
      }
    }, err => {
      this.isSending = false;
      this.toastr.error(err)
    });
  }

  updateRoleSelection(roles: Array<string>) {
    this.selectedRoles = roles;
  }

  updateLibrarySelection(libraries: Array<Library>) {
    this.selectedLibraries = libraries.map(l => l.id);
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

}
