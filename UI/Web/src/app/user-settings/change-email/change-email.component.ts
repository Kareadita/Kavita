import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {shareReplay, take} from 'rxjs';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { ApiKeyComponent } from '../api-key/api-key.component';
import { NgbTooltip, NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import {NgIf, NgFor, JsonPipe} from '@angular/common';
import {translate, TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-change-email',
    templateUrl: './change-email.component.html',
    styleUrls: ['./change-email.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgbTooltip, NgbCollapse, NgFor, ReactiveFormsModule, ApiKeyComponent, TranslocoDirective, JsonPipe]
})
export class ChangeEmailComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);

  form: FormGroup = new FormGroup({});
  user: User | undefined = undefined;
  errors: string[] = [];
  isViewMode: boolean = true;
  emailLink: string = '';
  emailConfirmed: boolean = true;
  hasValidEmail: boolean = true;


  public get email() { return this.form.get('email'); }

  makeLink: (val: string) => string = (val: string) => {return this.emailLink};

  constructor(public accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), take(1)).subscribe(user => {
      this.user = user;
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
      }

      if (updateEmailResponse.emailSent) {
        this.toastr.success(translate('toasts.email-sent-to'));
      } else {
        this.toastr.success(translate('toasts.change-email-no-email'));
        this.accountService.refreshAccount().subscribe(user => {
          this.user = user;
          this.form.get('email')?.setValue(this.user?.email);
          this.cdRef.markForCheck();
        });

      }

      this.isViewMode = true;
      this.resetForm();
    }, err => {
      this.errors = err;
    })
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }

}
