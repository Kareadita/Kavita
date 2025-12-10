import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  model,
  OnInit,
  signal
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {Library} from 'src/app/_models/library/library';
import {Member} from 'src/app/_models/auth/member';
import {AccountService, allRoles, Role} from 'src/app/_services/account.service';
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {RestrictionSelectorComponent} from '../../user-settings/restriction-selector/restriction-selector.component';
import {AsyncPipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {debounceTime, distinctUntilChanged, Observable, startWith, tap} from "rxjs";
import {map} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ServerSettings} from "../_models/server-settings";
import {IdentityProvider, IdentityProviders} from "../../_models/user/user";
import {IdentityProviderPipePipe} from "../../_pipes/identity-provider.pipe";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {LibraryService} from "../../_services/library.service";

const AllowedUsernameCharacters = /^[a-zA-Z0-9\-._@+/]*$/;
const EmailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

@Component({
    selector: 'app-edit-user',
    templateUrl: './edit-user.component.html',
    styleUrls: ['./edit-user.component.scss'],
  imports: [ReactiveFormsModule, RestrictionSelectorComponent, SentenceCasePipe, TranslocoDirective, AsyncPipe, IdentityProviderPipePipe, SettingMultiCheckBox],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly modal = inject(NgbActiveModal);
  private readonly libraryService = inject(LibraryService);

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
  isSaving: boolean = false;

  userForm: FormGroup = new FormGroup({});
  isEmailInvalid$!: Observable<boolean>;

  allowedCharacters = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/';

  public get email() { return this.userForm.get('email'); }
  public get username() { return this.userForm.get('username'); }
  public get password() { return this.userForm.get('password'); }
  get hasAdminRoleSelected() { return this.userForm.get('roles')!.value.includes(Role.Admin); };



  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));

    this.userForm.addControl('email', new FormControl(this.member().email, [Validators.required]));
    this.userForm.addControl('username', new FormControl(this.member().username, [Validators.required, Validators.pattern(AllowedUsernameCharacters)]));
    this.userForm.addControl('identityProvider', new FormControl(this.member().identityProvider, [Validators.required]));
    this.userForm.addControl('roles', new FormControl(this.member().roles));
    this.userForm.addControl('libraries', new FormControl(this.member().libraries.map(l => l.id)));

    this.userForm.get('identityProvider')!.valueChanges.pipe(
      tap(value => {
        const newIdentityProvider = parseInt(value, 10) as IdentityProvider;
        if (newIdentityProvider === IdentityProvider.OpenIdConnect) return;
        this.member.set({
          ...this.member(),
          identityProvider: newIdentityProvider,
        })
      })).subscribe();

    this.isEmailInvalid$ = this.userForm.get('email')!.valueChanges.pipe(
      startWith(this.member().email),
      distinctUntilChanged(),
      debounceTime(10),
      map(value => !EmailRegex.test(value)),
      takeUntilDestroyed(this.destroyRef)
    );

    this.selectedRestriction = this.member().ageRestriction;
    this.cdRef.markForCheck();
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close(false);
  }

  save() {
    const model = this.userForm.getRawValue();
    model.userId = this.member().id;
    model.ageRestriction = this.selectedRestriction;
    model.identityProvider = parseInt(model.identityProvider, 10) as IdentityProvider;


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
  protected readonly IdentityProviders = IdentityProviders;
}
