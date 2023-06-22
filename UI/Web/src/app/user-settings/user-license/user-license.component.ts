import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit, Output
} from '@angular/core';
import {FormControl, FormGroup, Validators} from "@angular/forms";
import {AccountService} from "../../_services/account.service";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-user-license',
  templateUrl: './user-license.component.html',
  styleUrls: ['./user-license.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserLicenseComponent implements OnInit {

  formGroup: FormGroup = new FormGroup({});
  isViewMode: boolean = true;
  private readonly destroyRef = inject(DestroyRef);

  hasValidLicense: boolean = false;
  hasLicense: boolean = false;
  isChecking: boolean = false;



  constructor(public accountService: AccountService, private scrobblingService: ScrobblingService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.formGroup.addControl('licenseKey', new FormControl('', [Validators.required]));
    this.formGroup.addControl('email', new FormControl('', [Validators.required]));
    this.accountService.hasAnyLicense().subscribe(res => {
      this.hasLicense = res;
      this.cdRef.markForCheck();
    });
    this.accountService.hasValidLicense().subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
    });
  }


  resetForm() {
    this.formGroup.get('licenseKey')?.setValue('');
    this.formGroup.get('email')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.accountService.updateUserLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim()).subscribe(isValid => {
      this.hasValidLicense = isValid;
      if (!this.hasValidLicense) {
        this.toastr.info("License Key saved, but it is not valid. Please ensure you have an active subscription");
      } else {
        this.toastr.success('KavitaPlus unlocked. Please reauthenticate to get full benefits.');
      }
      this.hasLicense = this.formGroup.get('licenseKey')!.value.length > 0;
      this.resetForm();
      this.isViewMode = true;
      this.cdRef.markForCheck();
    }, err => {

    });
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }

  validateLicense() {
    this.isChecking = true;
    this.accountService.hasValidLicense(true).subscribe(res => {
      this.hasValidLicense = res;
      this.isChecking = false;
      this.cdRef.markForCheck();
    })

  }

}
