import {ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import { CommonModule } from '@angular/common';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-edit-external-source-item',
  standalone: true,
  imports: [CommonModule, NgbCollapse, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './edit-external-source-item.component.html',
  styleUrls: ['./edit-external-source-item.component.scss']
})
export class EditExternalSourceItemComponent implements OnInit {

  @Input({required: true}) source!: ExternalSource;
  formGroup: FormGroup = new FormGroup({});
  isViewMode: boolean = true;
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);


  constructor(public accountService: AccountService, private toastr: ToastrService) {
    // this.accountService.hasValidLicense().subscribe(res => {
    //   this.hasValidLicense = res;
    //   this.cdRef.markForCheck();
    //   if (this.hasValidLicense) {
    //     this.scrobblingService.getAniListToken().subscribe(token => {
    //       this.token = token;
    //       this.formGroup.get('aniListToken')?.setValue(token);
    //       this.cdRef.markForCheck();
    //     });
    //     this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
    //       this.tokenExpired = hasExpired;
    //       this.cdRef.markForCheck();
    //     });
    //   }
    // });
  }

  ngOnInit(): void {
    this.formGroup.addControl('host', new FormControl('', [Validators.required]));
    this.formGroup.addControl('apiKey', new FormControl('', [Validators.required]));
  }



  resetForm() {
    this.formGroup.get('host')?.setValue('');
    this.formGroup.get('apiKey')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    // this.scrobblingService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(() => {
    //   this.toastr.success(translate('toasts.anilist-token-updated'));
    //   //this.token = this.formGroup.get('aniListToken')!.value;
    //   this.resetForm();
    //   this.isViewMode = true;
    //   this.cdRef.markForCheck();
    // });
    this.resetForm();
    this.isViewMode = true;
    this.cdRef.markForCheck();
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    this.resetForm();
  }
}
