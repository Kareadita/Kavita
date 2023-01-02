import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Observable, of, Subject, takeUntil, shareReplay, map, tap, take } from 'rxjs';
import { UpdateEmailResponse } from 'src/app/_models/auth/update-email-response';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-change-email',
  templateUrl: './change-email.component.html',
  styleUrls: ['./change-email.component.scss']
})
export class ChangeEmailComponent implements OnInit, OnDestroy {

  form: FormGroup = new FormGroup({});
  user: User | undefined = undefined;
  hasChangePasswordAbility: Observable<boolean> = of(false);
  passwordsMatch = false;
  errors: string[] = [];
  isViewMode: boolean = true;
  emailLink: string = '';
  emailConfirmed: boolean = true;

  public get email() { return this.form.get('email'); }

  private onDestroy = new Subject<void>();

  makeLink: (val: string) => string = (val: string) => {return this.emailLink};

  constructor(public accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), shareReplay(), take(1)).subscribe(user => {
      this.user = user;
      this.form.addControl('email', new FormControl(user?.email, [Validators.required, Validators.email]));
      this.cdRef.markForCheck();
      this.accountService.isEmailConfirmed().subscribe((confirmed) => {
        this.emailConfirmed = confirmed;
        this.cdRef.markForCheck();
      });
    });

    
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
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
    this.accountService.updateEmail(model.email).subscribe((updateEmailResponse: UpdateEmailResponse) => {
      if (updateEmailResponse.emailSent) {
        if (updateEmailResponse.hadNoExistingEmail) {
          this.toastr.success('An email has been sent to ' + model.email + ' for confirmation.');
        } else {
          this.toastr.success('An email has been sent to your old email address for confirmation');
        }
      } else {
        this.toastr.success('The server is not publicly accessible. Ask the admin to fetch your confirmation link from the logs');
      }
      
      this.resetForm();
      this.isViewMode = true;
    }, err => {
      this.errors = err;
    })
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }

}
