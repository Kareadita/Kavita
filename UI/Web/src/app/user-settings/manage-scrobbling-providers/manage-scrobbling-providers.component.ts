import {ChangeDetectorRef, Component, inject, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ScrobbleProviderItemComponent} from "../scrobble-provider-item/scrobble-provider-item.component";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {LicenseService} from "../../_services/license.service";
import {ConfirmService} from "../../shared/confirm.service";

@Component({
    selector: 'app-manage-scrobbling-providers',
    imports: [
        ReactiveFormsModule,
        TranslocoDirective,
        LoadingComponent,
        ScrobbleProviderItemComponent,
        ScrobbleProviderNamePipe,
        SettingTitleComponent,
    ],
    templateUrl: './manage-scrobbling-providers.component.html',
    styleUrl: './manage-scrobbling-providers.component.scss'
})
export class ManageScrobblingProvidersComponent implements OnInit {
  public readonly accountService = inject(AccountService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly licenseService = inject(LicenseService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly ScrobbleProvider = ScrobbleProvider;

  hasValidLicense = this.licenseService.hasActiveLicense;

  formGroup: FormGroup = new FormGroup({});
  aniListToken: string = '';
  malToken: string = '';
  malUsername: string = '';


  isViewMode = signal<boolean>(true);
  isLoaded = signal<boolean>(false);

  ngOnInit(): void {

    if (!this.hasValidLicense()) {
      this.isLoaded.set(true);
      return;
    }

    this.scrobblingService.getAniListToken().subscribe(token => {
      this.aniListToken = token;
      this.formGroup.get('aniListToken')?.setValue(token);
      this.isLoaded.set(true);
      this.cdRef.markForCheck();
    });
    this.scrobblingService.getMalToken().subscribe(dto => {
      this.malToken = dto.accessToken;
      this.malUsername = dto.username;
      this.formGroup.get('malToken')?.setValue(this.malToken);
      this.formGroup.get('malUsername')?.setValue(this.malUsername);
      this.cdRef.markForCheck();
    });

    this.formGroup.addControl('aniListToken', new FormControl('', [Validators.required]));
    this.formGroup.addControl('malClientId', new FormControl('', [Validators.required]));
    this.formGroup.addControl('malUsername', new FormControl('', [Validators.required]));
  }



  resetForm() {
    this.formGroup.get('aniListToken')?.setValue('');
    this.formGroup.get('malClientId')?.setValue('');
    this.formGroup.get('malUsername')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveAniListForm() {
    this.scrobblingService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(async (isFirstToken) => {

      if (isFirstToken) {
        const result = await this.confirmService.confirm('', {
          buttons: [
            {text: translate('scrobbling-providers.anilist-first-later'), type: 'secondary'},
            {text: translate('scrobbling-providers.anilist-first-now'), type: 'primary'},
          ],
          _type: 'confirm',
          content: translate('scrobbling-providers.anilist-first-description'),
          header: translate('scrobbling-providers.anilist-first-header'),
          disableEscape: true
        });
        // false is Later, true is Now
        if (result) {
          this.scrobblingService.triggerScrobbleEventGeneration().subscribe(_ => {
            this.aniListToken = this.formGroup.get('aniListToken')!.value;
            this.resetForm();
            this.cdRef.markForCheck();
          });
          return;
        }
      }

      this.toastr.success(translate('toasts.anilist-token-updated'));
      this.aniListToken = this.formGroup.get('aniListToken')!.value;
      this.resetForm();
      this.cdRef.markForCheck();
    });
  }

  saveMalForm() {
    this.scrobblingService.updateMalToken(this.formGroup.get('malUsername')!.value, this.formGroup.get('malClientId')!.value).subscribe(() => {
      this.toastr.success(translate('toasts.mal-clientId-updated'));
      this.malToken = this.formGroup.get('malClientId')!.value;
      this.malUsername = this.formGroup.get('malUsername')!.value;
      this.resetForm();
      this.cdRef.markForCheck();
    });
  }

  updateEditMode(mode: boolean) {
    this.isViewMode.set(!mode);
    this.resetForm();
    this.cdRef.markForCheck();
  }
}
