import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, inject,
  OnInit
} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from "@angular/forms";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../shared/confirm.service";
import { LoadingComponent } from '../../shared/loading/loading.component';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import {environment} from "../../../environments/environment";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {RouterLink} from "@angular/router";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
  selector: 'app-license',
  templateUrl: './license.component.html',
  styleUrls: ['./license.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [NgbTooltip, LoadingComponent, ReactiveFormsModule, TranslocoDirective, RouterLink, SettingItemComponent]
})
export class LicenseComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly accountService = inject(AccountService);
  protected readonly WikiLink = WikiLink;

  formGroup: FormGroup = new FormGroup({});
  isViewMode: boolean = true;

  hasValidLicense: boolean = false;
  hasLicense: boolean = false;
  isChecking: boolean = true;
  isSaving: boolean = false;

  buyLink = environment.buyLink;
  manageLink = environment.manageLink;




  ngOnInit(): void {
    this.formGroup.addControl('licenseKey', new FormControl('', [Validators.required]));
    this.formGroup.addControl('email', new FormControl('', [Validators.required]));
    this.formGroup.addControl('discordId', new FormControl('', [Validators.pattern(/\d+/)]));

    this.isChecking = true;
    this.cdRef.markForCheck();

    this.accountService.hasAnyLicense().subscribe(res => {
      this.hasLicense = res;
      this.cdRef.markForCheck();

      if (this.hasLicense) {
        this.accountService.hasValidLicense().subscribe(res => {
          this.hasValidLicense = res;
          this.isChecking = false;
          this.cdRef.markForCheck();
        });
      }
    });
  }


  resetForm() {
    this.formGroup.get('licenseKey')?.setValue('');
    this.formGroup.get('email')?.setValue('');
    this.formGroup.get('discordId')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.isSaving = true;
    this.cdRef.markForCheck();
    this.accountService.updateUserLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim(), this.formGroup.get('discordId')!.value.trim())
      .subscribe(() => {
      this.accountService.hasValidLicense(true).subscribe(isValid => {
        this.hasValidLicense = isValid;
        if (!this.hasValidLicense) {
          this.toastr.info(translate('toasts.k+-license-saved'));
        } else {
          this.toastr.success(translate('toasts.k+-unlocked'));
        }
        this.hasLicense = this.formGroup.get('licenseKey')!.value.length > 0;
        this.resetForm();
        this.isViewMode = true;
        this.isSaving = false;
        this.cdRef.markForCheck();
      });
    }, err => {
        this.isSaving = false;
        this.cdRef.markForCheck();
        if (err.hasOwnProperty('error')) {
          this.toastr.error(JSON.parse(err['error']));
        } else {
          this.toastr.error(translate('toasts.k+-error'));
        }
    });
  }

  async deleteLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-delete-key'))) {
      return;
    }

    this.accountService.deleteLicense().subscribe(() => {
      this.resetForm();
      this.toggleViewMode();
      this.validateLicense();
    });
  }

  async resetLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-reset-key'))) {
      return;
    }

    this.accountService.resetLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim()).subscribe(() => {
      this.toastr.success(translate('toasts.k+-reset-key-success'));
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

  updateEditMode(mode: boolean) {
    this.isViewMode = mode;
    this.cdRef.markForCheck();
  }
}
