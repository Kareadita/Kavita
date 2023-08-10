import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from "@angular/forms";
import {ToastrService} from "ngx-toastr";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {AccountService} from "../../_services/account.service";
import { NgbTooltip, NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import { NgIf, NgOptimizedImage } from '@angular/common';
import {translate, TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-anilist-key',
    templateUrl: './anilist-key.component.html',
    styleUrls: ['./anilist-key.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgIf, NgOptimizedImage, NgbTooltip, NgbCollapse, ReactiveFormsModule, TranslocoDirective]
})
export class AnilistKeyComponent implements OnInit {

  hasValidLicense: boolean = false;

  formGroup: FormGroup = new FormGroup({});
  token: string = '';
  isViewMode: boolean = true;
  private readonly destroyRef = inject(DestroyRef);
  tokenExpired: boolean = false;


  constructor(public accountService: AccountService, private scrobblingService: ScrobblingService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) {
    this.accountService.hasValidLicense().subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
      if (this.hasValidLicense) {
        this.scrobblingService.getAniListToken().subscribe(token => {
          this.token = token;
          this.formGroup.get('aniListToken')?.setValue(token);
          this.cdRef.markForCheck();
        });
        this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
          this.tokenExpired = hasExpired;
          this.cdRef.markForCheck();
        });
      }
    });
  }

  ngOnInit(): void {
    this.formGroup.addControl('aniListToken', new FormControl('', [Validators.required]));
  }



  resetForm() {
    this.formGroup.get('aniListToken')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.scrobblingService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(() => {
      this.toastr.success(translate('toasts.anilist-token-updated'));
      this.token = this.formGroup.get('aniListToken')!.value;
      this.resetForm();
      this.isViewMode = true;
    });
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }


}
