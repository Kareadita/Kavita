import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnChanges,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, Validators} from "@angular/forms";
import {ToastrService} from "ngx-toastr";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {AccountService} from "../../_services/account.service";

@Component({
  selector: 'app-anilist-key',
  templateUrl: './anilist-key.component.html',
  styleUrls: ['./anilist-key.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnilistKeyComponent implements OnInit, OnChanges {

  @Input({required: true}) hasValidLicense!: boolean;

  formGroup: FormGroup = new FormGroup({});
  token: string = '';
  isViewMode: boolean = true;
  private readonly destroyRef = inject(DestroyRef);
  tokenExpired: boolean = false;


  constructor(public accountService: AccountService, private scrobblingService: ScrobblingService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.formGroup.addControl('aniListToken', new FormControl('', [Validators.required]));
  }

  ngOnChanges() {
    if (this.hasValidLicense) {
      this.scrobblingService.getAniListToken().subscribe(token => {
        this.token = token;
        this.formGroup.get('aniListToken')?.setValue(token);
        this.cdRef.markForCheck();
      });
      this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
        this.tokenExpired = hasExpired;
        this.cdRef.markForCheck();
      })
    }
  }


  resetForm() {
    this.formGroup.get('aniListToken')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.scrobblingService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(() => {
      this.toastr.success('AniList Token has been updated');
      this.token = this.formGroup.get('aniListToken')!.value;
      this.resetForm();
      this.isViewMode = true;
    }, err => {

    });
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }


}
