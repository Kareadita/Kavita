import {ChangeDetectorRef, Component, ContentChild, DestroyRef, ElementRef, inject, OnInit} from '@angular/core';
import {NgIf, NgOptimizedImage} from "@angular/common";
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse, NgbAccordionDirective, NgbAccordionHeader, NgbAccordionItem,
  NgbCollapse,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {AccountService} from "../../_services/account.service";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";
import {ManageAlertsComponent} from "../../admin/manage-alerts/manage-alerts.component";
import {LoadingComponent} from "../../shared/loading/loading.component";

@Component({
  selector: 'app-manage-scrobbling-providers',
  standalone: true,
  imports: [
    NgIf,
    NgOptimizedImage,
    NgbTooltip,
    ReactiveFormsModule,
    Select2Module,
    TranslocoDirective,
    NgbCollapse,
    ManageAlertsComponent,
    NgbAccordionBody,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionDirective,
    NgbAccordionHeader,
    NgbAccordionItem,
    LoadingComponent,
  ],
  templateUrl: './manage-scrobbling-providers.component.html',
  styleUrl: './manage-scrobbling-providers.component.scss'
})
export class ManageScrobblingProvidersComponent implements OnInit {
  public readonly accountService = inject(AccountService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  hasValidLicense: boolean = false;

  formGroup: FormGroup = new FormGroup({});
  aniListToken: string = '';
  malToken: string = '';
  malUsername: string = '';

  aniListTokenExpired: boolean = false;
  malTokenExpired: boolean = false;

  isViewMode: boolean = true;
  loaded: boolean = false;

  constructor() {
    this.accountService.hasValidLicense$.subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
      if (this.hasValidLicense) {
        this.scrobblingService.getAniListToken().subscribe(token => {
          this.aniListToken = token;
          this.formGroup.get('aniListToken')?.setValue(token);
          this.loaded = true;
          this.cdRef.markForCheck();
        });
        this.scrobblingService.getMalToken().subscribe(dto => {
          this.malToken = dto.accessToken;
          this.malUsername = dto.username;
          this.formGroup.get('malToken')?.setValue(this.malToken);
          this.formGroup.get('malUsername')?.setValue(this.malUsername);
          this.cdRef.markForCheck();
        });
        this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
          this.aniListTokenExpired = hasExpired;
          this.cdRef.markForCheck();
        });
      }
    });
  }

  ngOnInit(): void {
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
    this.scrobblingService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(() => {
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

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }
}
