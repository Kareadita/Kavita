import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { SafeUrl } from '@angular/platform-browser';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-add-email-to-account-migration-modal',
  templateUrl: './add-email-to-account-migration-modal.component.html',
  styleUrls: ['./add-email-to-account-migration-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AddEmailToAccountMigrationModalComponent implements OnInit {

  @Input() username!: string;
  @Input() password!: string;

  isSaving: boolean = false;
  registerForm: FormGroup = new FormGroup({});
  emailLink: string = '';
  emailLinkUrl: SafeUrl | undefined;
  error: string = '';

  constructor(private accountService: AccountService, private modal: NgbActiveModal, 
    private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    this.registerForm.addControl('username', new FormControl(this.username, [Validators.required]));
    this.registerForm.addControl('email', new FormControl('', [Validators.required, Validators.email]));
    this.registerForm.addControl('password', new FormControl(this.password, [Validators.required, Validators.minLength(6), Validators.maxLength(32)]));
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close(false);
  }

  save() {
    const model = this.registerForm.getRawValue();
    model.sendEmail = false;
    this.accountService.migrateUser(model).subscribe(async () => {
      this.toastr.success('Email has been validated');
      this.modal.close(true);
    }, err => {
      this.error = err;
    });
  }
}
