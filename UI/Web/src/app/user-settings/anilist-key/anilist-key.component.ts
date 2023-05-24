import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, Validators} from "@angular/forms";
import {User} from "../../_models/user";
import {shareReplay, take} from "rxjs";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-anilist-key',
  templateUrl: './anilist-key.component.html',
  styleUrls: ['./anilist-key.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnilistKeyComponent implements OnInit {

  formGroup: FormGroup = new FormGroup({});
  token: string = '';
  isViewMode: boolean = true;
  private readonly destroyRef = inject(DestroyRef);


  constructor(private accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.formGroup.addControl('aniListToken', new FormControl('', [Validators.required]));
    this.accountService.getAniListToken().subscribe(token => {
      this.token = token;
      this.formGroup.get('aniListToken')?.setValue(token);
      this.cdRef.markForCheck();
    });
  }


  resetForm() {
    this.formGroup.get('aniListToken')?.setValue('');
    this.cdRef.markForCheck();
  }

  saveForm() {
    this.accountService.updateAniListToken(this.formGroup.get('aniListToken')!.value).subscribe(() => {
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
