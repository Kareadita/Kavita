import { Component, Input, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { Library } from 'src/app/_models/library';
import { Member } from 'src/app/_models/auth/member';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-edit-user',
  templateUrl: './edit-user.component.html',
  styleUrls: ['./edit-user.component.scss']
})
export class EditUserComponent implements OnInit {

  @Input() member!: Member;
  
  selectedRoles: Array<string> = [];
  selectedLibraries: Array<number> = [];
  selectedRestriction!: AgeRestriction;
  isSaving: boolean = false;

  userForm: FormGroup = new FormGroup({});

  public get email() { return this.userForm.get('email'); }
  public get username() { return this.userForm.get('username'); }
  public get password() { return this.userForm.get('password'); }
  public get hasAdminRoleSelected() { return this.selectedRoles.includes('Admin'); };

  constructor(public modal: NgbActiveModal, private accountService: AccountService) { }

  ngOnInit(): void {
    this.userForm.addControl('email', new FormControl(this.member.email, [Validators.required, Validators.email]));
    this.userForm.addControl('username', new FormControl(this.member.username, [Validators.required]));

    this.userForm.get('email')?.disable();
    this.selectedRestriction = this.member.ageRestriction;
  }

  updateRoleSelection(roles: Array<string>) {
    this.selectedRoles = roles;
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  updateLibrarySelection(libraries: Array<Library>) {
    this.selectedLibraries = libraries.map(l => l.id);
  }

  close() {
    this.modal.close(false);
  }

  save() {
    const model = this.userForm.getRawValue();
    model.userId = this.member.id;
    model.roles = this.selectedRoles;
    model.libraries = this.selectedLibraries;
    model.ageRestriction = this.selectedRestriction;
    this.accountService.update(model).subscribe(() => {
      this.modal.close(true);
    });
  }

}
