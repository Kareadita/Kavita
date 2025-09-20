import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {shareReplay} from 'rxjs';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ApiKeyComponent} from '../api-key/api-key.component';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

@Component({
  selector: 'app-change-email',
  templateUrl: './change-email.component.html',
  styleUrls: ['./change-email.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, ReactiveFormsModule, ApiKeyComponent, TranslocoDirective, SettingItemComponent, DefaultValuePipe]
})
export class ChangeEmailComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  form: FormGroup = new FormGroup({});
  user: User | undefined = undefined;
  errors: string[] = [];
  isEditMode: boolean = false;
  emailLink: string = '';
  emailConfirmed: boolean = true;
  hasValidEmail: boolean = true;
  canEdit: boolean = false;


  protected get email() { return this.form.get('email'); }

  makeLink: (val: string) => string = (val: string) => {return this.emailLink};


  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay()).subscribe(user => {
      this.user = user!;
      this.canEdit = !this.accountService.hasReadOnlyRole(user!);
      this.form.addControl('email', new FormControl(user?.email, [Validators.required, Validators.email]));
      this.form.addControl('password', new FormControl('', [Validators.required]));
      this.cdRef.markForCheck();


      this.accountService.isEmailConfirmed().subscribe((confirmed) => {
        this.emailConfirmed = confirmed;
        this.cdRef.markForCheck();
      });

      this.accountService.isEmailValid().subscribe(isValid => {
        this.hasValidEmail = isValid;
        this.cdRef.markForCheck();
      });
    });
  }

  resetForm() {
    this.form.get('email')?.setValue(this.user?.email);
    this.errors = [];
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.user === undefined) { return; }

    const model = this.form.value;
    this.errors = [];

    this.accountService.updateEmail(model.email, model.password).subscribe(updateEmailResponse => {
      if (updateEmailResponse.invalidEmail) {
        this.toastr.success(translate('toasts.email-sent-to-no-existing', {email: model.email}));
      } else if (updateEmailResponse.emailSent) {
        this.toastr.success(translate('toasts.email-sent-to'));
      } else {
        this.toastr.success(translate('toasts.change-email-no-email'));
      }

      this.accountService.refreshAccount().subscribe(user => {
        this.user = user;
        this.resetForm();
        this.cdRef.markForCheck();
      });
      this.isEditMode = false;
      this.cdRef.markForCheck();
    }, err => {
      this.errors = err;
      this.cdRef.markForCheck();
    })
  }

  updateEditMode(val: boolean) {
    this.isEditMode = val;
    this.cdRef.markForCheck();
  }
}
