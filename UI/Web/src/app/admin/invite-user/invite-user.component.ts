import {ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { InviteUserResponse } from 'src/app/_models/auth/invite-user-response';
import { Library } from 'src/app/_models/library/library';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { AccountService } from 'src/app/_services/account.service';
import { ApiKeyComponent } from '../../user-settings/api-key/api-key.component';
import { RestrictionSelectorComponent } from '../../user-settings/restriction-selector/restriction-selector.component';
import { LibrarySelectorComponent } from '../library-selector/library-selector.component';
import { RoleSelectorComponent } from '../role-selector/role-selector.component';
import { NgIf } from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";

@Component({
    selector: 'app-invite-user',
    templateUrl: './invite-user.component.html',
    styleUrls: ['./invite-user.component.scss'],
    standalone: true,
    imports: [NgIf, ReactiveFormsModule, RoleSelectorComponent, LibrarySelectorComponent, RestrictionSelectorComponent, ApiKeyComponent, TranslocoDirective, SafeHtmlPipe]
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
  invited: boolean = false;
  inviteError: boolean = false;

  private readonly cdRef = inject(ChangeDetectorRef);

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
      this.invited = true;
      this.cdRef.markForCheck();

      if (data.invalidEmail) {
        this.toastr.info(translate('toasts.email-not-sent'));
        this.inviteError = true;
        this.cdRef.markForCheck();
        return;
      }

      if (data.emailSent) {
        this.toastr.info(translate('toasts.email-sent', {email: email}));
        this.modal.close(true);
      }

    }, err => {
      // Note to self: If you need to catch an error, do it, but don't toast because interceptor handles that
      this.isSending = false;
      this.cdRef.markForCheck();
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
