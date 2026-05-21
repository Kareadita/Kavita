import {ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, inject, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {LicenseInfo} from "../../../_models/kavitaplus/license-info";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {SettingButtonComponent} from "../../../settings/_components/setting-button/setting-button.component";
import {DecimalPipe} from "@angular/common";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../../shared/confirm.service";
import {AccountService} from "../../../_services/account.service";
import {LicenseService} from "../../../_services/license.service";
import {environment} from "../../../../environments/environment";
import {WikiLink} from "../../../_models/wiki";
import {switchMap} from "rxjs/operators";
import {filter, tap} from "rxjs";
import {
  KavitaPlusRegistrationErrorCode
} from '../../../_models/kavitaplus/registration/kavita-plus-registration-error-code';
import {RegisterLicenseKeyComponent} from "../register-license-key/register-license-key.component";
import {KavitaPlusUpsellComponent} from "../kavita-plus-upsell/kavita-plus-upsell.component";

@Component({
    selector: 'app-license',
    templateUrl: './license.component.html',
    styleUrls: ['./license.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, LoadingComponent, ReactiveFormsModule, TranslocoDirective, SettingItemComponent,
    DefaultValuePipe, UtcToLocalTimePipe, SettingButtonComponent, DecimalPipe, RegisterLicenseKeyComponent, KavitaPlusUpsellComponent]
})
export class LicenseComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);

  protected readonly WikiLink = WikiLink;
  protected readonly buyLink = environment.buyLink;

  formGroup: FormGroup = new FormGroup({
    'licenseKey': new FormControl('', [Validators.required]),
    'email': new FormControl('', [Validators.required]),
    'discordId': new FormControl('', [Validators.pattern(/\d+/)])
  });
  isViewMode = signal<boolean>(true);
  isChecking = signal<boolean>(true);
  isSaving = signal<boolean>(false);
  hasLicense = signal<boolean>(false);
  licenseInfo = signal<LicenseInfo | null>(null);
  showEmail = signal<boolean>(false);

  /**
   * Either the normal manageLink or with a prefilled email to ease the user
   */
  readonly manageLink = computed(() => {
    const email = this.licenseInfo()?.registeredEmail;
    if (!email) return environment.manageLink;

    return environment.manageLink + '?prefilled_email=' + encodeURIComponent(email);
  });




  ngOnInit(): void {
    this.loadLicenseInfo();
  }

  loadLicenseInfo(forceCheck = false) {
    this.getLicenseInfoObservable(forceCheck).subscribe();
  }

  getLicenseInfoObservable(forceCheck = false) {
    this.isChecking.set(true);

    return this.licenseService.hasAnyLicense()
      .pipe(
        tap(res => {
          this.hasLicense.set(res);
          this.isChecking.set(false);
        }),
        filter(hasLicense => hasLicense),
        tap(_ => {
          this.isChecking.set(true);
        }),
        switchMap(_ => this.licenseService.licenseInfo(forceCheck)),
        tap(licenseInfo => {
          this.licenseInfo.set(licenseInfo);
          this.isChecking.set(false);
        })
      );
  }


  resetForm() {
    this.formGroup.get('licenseKey')?.setValue('');
    this.formGroup.get('email')?.setValue('');
    this.formGroup.get('discordId')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.isSaving.set(true);

    const hadActiveLicenseBefore = this.licenseInfo()?.isActive;

    const license = this.formGroup.get('licenseKey')!.value.trim();
    const email = this.formGroup.get('email')!.value.trim();
    const discordId = this.formGroup.get('discordId')!.value.trim();

    this.licenseService.updateUserLicense(license, email, discordId)
      .subscribe({
        next: async (result) => {
          if (!result.success) {
            this.isSaving.set(false);
            this.cdRef.markForCheck();
            if (result.errorCode === KavitaPlusRegistrationErrorCode.AlreadyRegistered) {
              const answer = await this.confirmService.confirm(translate('license.k+-license-overwrite'), {
                _type: 'confirm',
                content: translate('license.k+-license-overwrite'),
                disableEscape: false,
                header: translate('license.k+-already-registered-header'),
                buttons: [
                  {text: translate('license.overwrite'), type: 'primary'},
                  {text: translate('license.cancel'), type: 'secondary'},
                ]
              });
              if (answer) {
                this.forceSave();
              }
            } else {
              const key = this.errorCodeI18nKey(result.errorCode);
              this.toastr.error(translate(key));
            }
            return;
          }
          this.resetForm();
          this.isViewMode.set(true);
          this.isSaving.set(false);
          this.cdRef.markForCheck();
          this.getLicenseInfoObservable().subscribe(async (info) => {
            if (info?.isActive && !hadActiveLicenseBefore) {
              await this.confirmService.info(translate('license.k+-unlocked-description'), translate('license.k+-unlocked'));
            } else {
              this.toastr.info(translate('toasts.k+-license-saved'));
            }
          });
        },
      });
  }

  private errorCodeI18nKey(code: KavitaPlusRegistrationErrorCode | undefined): string {
    switch (code) {
      case KavitaPlusRegistrationErrorCode.SubscriptionInactive:
        return 'registration-error-code-pipe.subscription-inactive';
      case KavitaPlusRegistrationErrorCode.InternalError:
        return 'registration-error-code-pipe.internal-error';
      default:
        return 'registration-error-code-pipe.registration-failed';
    }
  }

  forceSave() {
    this.isSaving.set(false);

    this.licenseService.resetLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim())
      .subscribe(_ => {
        this.saveForm();
      });
  }

  async deleteLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-delete-key'))) {
      return;
    }

    this.licenseService.deleteLicense().subscribe(() => {
      this.resetForm();
      this.isViewMode.set(true);
      this.licenseInfo.set(null);
      this.hasLicense.set(false);
      this.cdRef.markForCheck();
    });
  }

  async resetLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-reset-key'))) {
      return;
    }

    this.licenseService.resetLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim()).subscribe(() => {
      this.toastr.success(translate('toasts.k+-reset-key-success'));
    });
  }

  resendWelcomeEmail() {
    this.licenseService.resendLicense().subscribe(res => {
      if (res) {
        this.toastr.success(translate('toasts.k+-resend-welcome-email-success'));
      } else {
        this.toastr.error(translate('toasts.k+-resend-welcome-message-error'));
      }

    })
  }

  updateEditMode(mode: boolean) {
    this.isViewMode.set(!mode);
  }

  toggleViewMode() {
    this.isViewMode.update(v => !v);
    this.resetForm();
  }

  toggleEmailShow() {
    this.showEmail.update(v => !v);
  }
}
