import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef, inject,
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
import {AsyncPipe, DecimalPipe} from "@angular/common";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {of, shareReplay, startWith, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {LicenseInfo} from "../../_models/kavitaplus/license-info";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ServerService} from "../../_services/server.service";
import {filter, tap} from "rxjs/operators";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {Action} from "../../_services/action-factory.service";
import {SettingButtonComponent} from "../../settings/_components/setting-button/setting-button.component";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {ApiKeyComponent} from "../../user-settings/api-key/api-key.component";
import {LicenseService} from "../../_services/license.service";

@Component({
  selector: 'app-license',
  templateUrl: './license.component.html',
  styleUrls: ['./license.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [NgbTooltip, LoadingComponent, ReactiveFormsModule, TranslocoDirective, SettingItemComponent,
    DefaultValuePipe, UtcToLocalTimePipe, SettingButtonComponent, DecimalPipe, ApiKeyComponent]
})
export class LicenseComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  protected readonly WikiLink = WikiLink;

  formGroup: FormGroup = new FormGroup({});
  isViewMode: boolean = true;
  isChecking: boolean = true;
  isSaving: boolean = false;



  hasValidLicense: boolean = false;
  hasLicense: boolean = false;
  licenseInfo: LicenseInfo | null = null;
  showEmail: boolean = false;

  buyLink = environment.buyLink;
  manageLink = environment.manageLink;


  ngOnInit(): void {
    this.formGroup.addControl('licenseKey', new FormControl('', [Validators.required]));
    this.formGroup.addControl('email', new FormControl('', [Validators.required]));
    this.formGroup.addControl('discordId', new FormControl('', [Validators.pattern(/\d+/)]));

    this.checkForLicense()
      .pipe(
        filter(hasLicense => hasLicense),
        tap(hasLicense => console.log('hasLicense: ', hasLicense)),
        switchMap(_ => this.validateLicense())
      )
      .subscribe();
  }

  private checkForLicense() {
    this.isChecking = true;
    this.cdRef.markForCheck();
    return this.licenseService.hasAnyLicense().pipe(tap(res => {
      this.hasLicense = res;
      this.isChecking = false;
      this.cdRef.markForCheck();
    }));
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
    this.licenseService.updateUserLicense(this.formGroup.get('licenseKey')!.value.trim(), this.formGroup.get('email')!.value.trim(), this.formGroup.get('discordId')!.value.trim())
      .subscribe(() => {
      this.licenseService.hasValidLicense(true).subscribe(isValid => {
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
        // TODO: If there is the already registered error, then prompt the user if they'd like to override their previous installation registration (aka reset then re-save)

        if (err.hasOwnProperty('error')) {
          if (err['error'][0] === '{') {
            this.toastr.error(JSON.parse(err['error']));
          } else {
            this.toastr.error(err['error']);
          }
        } else {
          this.toastr.error(translate('toasts.k+-error'));
        }
    });
  }

  async deleteLicense() {
    if (!await this.confirmService.confirm(translate('toasts.k+-delete-key'))) {
      return;
    }

    this.licenseService.deleteLicense().subscribe(() => {
      this.resetForm();
      this.isViewMode = true;
      this.licenseInfo = null;
      this.hasLicense = false;
      this.hasValidLicense = false;
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


  validateLicense(forceCheck = false) {
    return of().pipe(
      startWith(null),
      tap(_ => {
        this.isChecking = true;
        this.cdRef.markForCheck();
      }),
      switchMap(_ => this.licenseService.licenseInfo(forceCheck)),
      tap(licenseInfo => {
        this.licenseInfo = licenseInfo;
        this.cdRef.markForCheck();
      })
    )

  }

  updateEditMode(mode: boolean) {
    this.isViewMode = !mode;
    this.cdRef.markForCheck();
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    console.log('edit mode: ', !this.isViewMode)
    this.cdRef.markForCheck();
    this.resetForm();
  }

  toggleEmailShow() {
    this.showEmail = !this.showEmail;
    this.cdRef.markForCheck();
  }
}
