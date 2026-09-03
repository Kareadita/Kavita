import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from '@openng/ngx-toastr';
import {ApiKeyComponent} from '../../user-settings/api-key/api-key.component';
import {RestrictionSelectorComponent} from '../../user-settings/restriction-selector/restriction-selector.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {LibraryService} from "../../_services/library.service";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {AccountService, allRoles, Role} from "../../_services/account.service";
import {AgeRestriction} from "../../_models/metadata/age-restriction";
import {AgeRating} from "../../_models/metadata/age-rating";
import {Library} from "../../_models/library/library";
import {InviteUserResponse} from "../../_models/auth/invite-user-response";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, required} from "@angular/forms/signals";

interface InviteFormModel {
  email: string;
  libraries: number[],
  roles: Role[]
}


@Component({
  selector: 'app-invite-user',
  templateUrl: './invite-user.component.html',
  styleUrls: ['./invite-user.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RestrictionSelectorComponent, ApiKeyComponent, TranslocoDirective, SafeHtmlPipe,
    SettingMultiCheckBox, FormFieldDirective, ValidationErrorsComponent, FormField]
})
export class InviteUserComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  protected readonly modal = inject(NgbActiveModal);
  private readonly libraryService = inject(LibraryService);

  /**
   * Maintains if the backend is sending an email
   */
  isSending = signal<boolean>(false);

  private readonly inviteFormModel = signal<InviteFormModel>({
    email: '',
    libraries: [],
    roles: [Role.Login]
  });
  inviteForm = form(this.inviteFormModel, (path) => {
    required(path.email);
  })

  selectedRestriction: AgeRestriction = {ageRating: AgeRating.NotApplicable, includeUnknowns: false};
  emailLink = signal<string>('');
  invited = signal<boolean>(false);
  inviteError = signal<boolean>(false);

  libraries = signal<Library[]>([]);
  libraryOptions = computed<MultiCheckBoxItem<number>[]>(() => this.libraries().map(l => {
    return { label: l.name, value: l.id };
  }));
  roleOptions: MultiCheckBoxItem<Role>[] = allRoles.map(r => {
    return { label: r, value: r, disableFunc: (r: Role, selected: Role[]) => {
      return r !== Role.Admin && selected.includes(Role.Admin);
      }}
  });

  readOnlyWarning = computed(() => {
    const roles = this.inviteForm.roles().value();
    return roles.includes(Role.ReadOnly)
      ? translate('edit-user.warning-read-only')
      : undefined;
  });

  canSave = computed(() => {
    return this.isSending() || !this.inviteForm().valid  || this.emailLink() !== '';
  });

  hasAdminRoleSelected = computed(() => {
    return this.inviteForm.roles().value().includes(Role.Admin);
  });



  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));
  }

  close() {
    this.modal.close(false);
  }

  invite() {
    this.isSending.set(true);

    const email = this.inviteForm.email().value();

    this.accountService.inviteUser({
      ...this.inviteFormModel(),
      ageRestriction: this.selectedRestriction
    }).subscribe((data: InviteUserResponse) => {
      this.emailLink.set(data.emailLink);
      this.isSending.set(false);
      this.invited.set(true);

      if (data.invalidEmail) {
        this.toastr.info(translate('toasts.email-not-sent'));
        this.inviteError.set(true);
        return;
      }

      if (data.emailSent) {
        this.toastr.info(translate('toasts.email-sent', {email: email}));
        this.modal.close(true);
      }

    }, err => {
      // Note to self: If you need to catch an error, do it, but don't toast because interceptor handles that
      this.isSending.set(false);
    });
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

}
