import {ChangeDetectorRef, Component, computed, inject, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {InviteUserResponse} from 'src/app/_models/auth/invite-user-response';
import {Library} from 'src/app/_models/library/library';
import {AgeRating} from 'src/app/_models/metadata/age-rating';
import {AccountService, allRoles, Role} from 'src/app/_services/account.service';
import {ApiKeyComponent} from '../../user-settings/api-key/api-key.component';
import {RestrictionSelectorComponent} from '../../user-settings/restriction-selector/restriction-selector.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {LibraryService} from "../../_services/library.service";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";

@Component({
    selector: 'app-invite-user',
    templateUrl: './invite-user.component.html',
    styleUrls: ['./invite-user.component.scss'],
  imports: [ReactiveFormsModule,RestrictionSelectorComponent,
    ApiKeyComponent, TranslocoDirective, SafeHtmlPipe, SettingMultiCheckBox]
})
export class InviteUserComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  protected readonly modal = inject(NgbActiveModal);
  private readonly libraryService = inject(LibraryService);

  /**
   * Maintains if the backend is sending an email
   */
  isSending: boolean = false;
  inviteForm: FormGroup<{
    email: FormControl<string>,
    libraries: FormControl<number[]>,
    roles: FormControl<Role[]>,
  }> = new FormGroup({
    email: new FormControl<string>(''),
    libraries: new FormControl<number[]>([]),
    roles: new FormControl<Role[]>([Role.Login]),
  }) as any;
  selectedRestriction: AgeRestriction = {ageRating: AgeRating.NotApplicable, includeUnknowns: false};
  emailLink: string = '';
  invited: boolean = false;
  inviteError: boolean = false;

  libraries = signal<Library[]>([]);
  libraryOptions = computed<MultiCheckBoxItem<number>[]>(() => this.libraries().map(l => {
    return { label: l.name, value: l.id };
  }));
  roleOptions: MultiCheckBoxItem<Role>[] = allRoles.map(r => {
    return { label: r, value: r, disableFunc: (r: Role, selected: Role[]) => {
      return r !== Role.Admin && selected.includes(Role.Admin);
      }}
  });


  makeLink: (val: string) => string = (_: string) => {return this.emailLink};

  get hasAdminRoleSelected() { return this.inviteForm.get('roles')!.value.includes(Role.Admin); };

  get email() { return this.inviteForm.get('email'); }


  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));
  }

  close() {
    this.modal.close(false);
  }

  invite() {
    this.isSending = true;

    const email = this.inviteForm.get('email')!.value;

    this.accountService.inviteUser({
      ...this.inviteForm.getRawValue(),
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

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
    this.cdRef.markForCheck();
  }

}
