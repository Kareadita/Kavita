import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {Library} from 'src/app/_models/library/library';
import {Member} from 'src/app/_models/auth/member';
import {AccountService} from 'src/app/_services/account.service';
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {RestrictionSelectorComponent} from '../../user-settings/restriction-selector/restriction-selector.component';
import {LibrarySelectorComponent} from '../library-selector/library-selector.component';
import {RoleSelectorComponent} from '../role-selector/role-selector.component';
import {NgIf} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";

const AllowedUsernameCharacters = /^[\sa-zA-Z0-9\-._@+/\s]*$/;

@Component({
    selector: 'app-edit-user',
    templateUrl: './edit-user.component.html',
    styleUrls: ['./edit-user.component.scss'],
    standalone: true,
  imports: [ReactiveFormsModule, NgIf, RoleSelectorComponent, LibrarySelectorComponent, RestrictionSelectorComponent, SentenceCasePipe, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly modal = inject(NgbActiveModal);

  @Input({required: true}) member!: Member;

  selectedRoles: Array<string> = [];
  selectedLibraries: Array<number> = [];
  selectedRestriction!: AgeRestriction;
  isSaving: boolean = false;

  userForm: FormGroup = new FormGroup({});

  allowedCharacters = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/';

  public get email() { return this.userForm.get('email'); }
  public get username() { return this.userForm.get('username'); }
  public get password() { return this.userForm.get('password'); }
  public get hasAdminRoleSelected() { return this.selectedRoles.includes('Admin'); };



  ngOnInit(): void {
    this.userForm.addControl('email', new FormControl(this.member.email, [Validators.required, Validators.email]));
    this.userForm.addControl('username', new FormControl(this.member.username, [Validators.required, Validators.pattern(AllowedUsernameCharacters)]));

    this.selectedRestriction = this.member.ageRestriction;
    this.cdRef.markForCheck();
  }

  updateRoleSelection(roles: Array<string>) {
    this.selectedRoles = roles;
    this.cdRef.markForCheck();
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
    this.cdRef.markForCheck();
  }

  updateLibrarySelection(libraries: Array<Library>) {
    this.selectedLibraries = libraries.map(l => l.id);
    this.cdRef.markForCheck();
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
