import {ChangeDetectionStrategy, Component, computed, effect, inject, model, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {RestrictionSelectorComponent} from '../../user-settings/restriction-selector/restriction-selector.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ServerSettings} from "../_models/server-settings";
import {allIdentityProviders, IdentityProvider} from "../../_models/user/user";
import {IdentityProviderPipePipe} from "../../_pipes/identity-provider.pipe";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {LibraryService} from "../../_services/library.service";
import {AccountService, allRoles, Role} from "../../_services/account.service";
import {Member} from "../../_models/auth/member";
import {Library} from "../../_models/library/library";
import {AgeRestriction} from "../../_models/metadata/age-restriction";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {form, FormField, pattern, required} from "@angular/forms/signals";
import {UpdateUserRequest} from "../../_models/user/update-user-request";

const AllowedUsernameCharacters = /^[a-zA-Z0-9\-._@+/]*$/;
const EmailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface EditUserForm {
  email: string;
  username: string;
  identityProvider: string;
  roles: Role[];
  libraries: number[];
}

@Component({
  selector: 'app-edit-user',
  templateUrl: './edit-user.component.html',
  styleUrls: ['./edit-user.component.scss'],
  imports: [RestrictionSelectorComponent, SentenceCasePipe, TranslocoDirective,
    IdentityProviderPipePipe, SettingMultiCheckBox, ValidationErrorsComponent, FormFieldDirective, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);
  protected readonly modal = inject(NgbActiveModal);

  member = model.required<Member>();
  settings = model.required<ServerSettings>();

  isLocked = computed(() => {
    const setting = this.settings();
    const member = this.member();
    return setting.oidcConfig.syncUserSettings && member.identityProvider === IdentityProvider.OpenIdConnect;
  });

  libraries = signal<Library[]>([]);
  libraryOptions = computed<MultiCheckBoxItem<number>[]>(() => this.libraries().map(l => {
    return { label: l.name, value: l.id };
  }));
  roleOptions: MultiCheckBoxItem<Role>[] = allRoles.map(r => {
    return { label: r, value: r, disableFunc: (r: Role, selected: Role[]) => {
        return r !== Role.Admin && selected.includes(Role.Admin);
      }}
  });

  selectedRestriction!: AgeRestriction;
  isSaving = signal(false);


  readonly allowedCharacters = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/';

  private readonly userFormModel = signal<EditUserForm>({
    email: '',
    username: '',
    roles: [],
    identityProvider: IdentityProvider.Kavita.toString(),
    libraries: []
  });
  userForm = form(this.userFormModel, (schemaPath) => {
    required(schemaPath.email);
    pattern(schemaPath.email, EmailRegex);

    required(schemaPath.username);
    pattern(schemaPath.username, AllowedUsernameCharacters);

    required(schemaPath.identityProvider);
  });

  hasAdminRoleSelected = computed(() => {
    return this.userForm.roles().value().includes(Role.Admin);
  });

  readOnlyWarning = computed(() => {
    const roles = this.userForm.roles().value();
    return roles.includes(Role.Admin)
      ? translate('edit-user.warning-read-only')
      : undefined;
  });

  constructor() {
    effect(() => {
      const newIdentityProvider = parseInt(this.userForm.identityProvider().value(), 10) as IdentityProvider;
      if (newIdentityProvider === IdentityProvider.OpenIdConnect) return;
      this.member.update(m => ({
        ...m,
        identityProvider: newIdentityProvider,
      }));
    });

  }


  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));

    this.userForm.email().value.set(this.member().email);
    this.userForm.username().value.set(this.member().username);
    this.userForm.identityProvider().value.set(this.member().identityProvider.toString());
    this.userForm.roles().value.set(this.member().roles);
    this.userForm.libraries().value.set(this.member().libraries.map(l => l.id));

    this.selectedRestriction = this.member().ageRestriction;
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  close() {
    this.modal.close(false);
  }

  save() {
    const model = {
      ...this.userFormModel(),
      userId: this.member().id,
      ageRestriction: this.selectedRestriction
    } as UpdateUserRequest;
    this.isSaving.set(true);

    this.accountService.update(model).subscribe({
      next: () => {
        this.modal.close(true);
      },
      error: err => {
        console.error(err);
      }
    });
  }

  protected readonly IdentityProvider = IdentityProvider;
  protected readonly IdentityProviders = allIdentityProviders;
}
