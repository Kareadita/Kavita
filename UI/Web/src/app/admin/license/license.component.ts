import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, Validators} from "@angular/forms";
import {AccountService} from "../../_services/account.service";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-license',
  templateUrl: './license.component.html',
  styleUrls: ['./license.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LicenseComponent implements OnInit {

  formGroup: FormGroup = new FormGroup({});
  isViewMode: boolean = true;

  hasValidLicense: boolean = false;
  hasLicense: boolean = false;
  isChecking: boolean = false;
  isSaving: boolean = false;



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
    this.isSaving = true;
    this.cdRef.markForCheck();
    this.accountService.updateUserLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim())
      .subscribe(() => {
      this.accountService.hasValidLicense(true).subscribe(isValid => {
        this.hasValidLicense = isValid;
        if (!this.hasValidLicense) {
          this.toastr.info("License Key saved, but it is not valid. Click check to revalidate the subscription. First time registration may take a min to propagate.");
        } else {
          this.toastr.success('Kavita+ unlocked!');
        }
        this.hasLicense = this.formGroup.get('licenseKey')!.value.length > 0;
        this.resetForm();
        this.isViewMode = true;
        this.cdRef.markForCheck();
        this.isSaving = false;
      });
    }, err => {
      this.toastr.error("There was an error when activating your license. Please try again.");
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
    });
  }
}
