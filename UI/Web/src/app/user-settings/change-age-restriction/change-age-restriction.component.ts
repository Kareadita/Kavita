import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
import {shareReplay, take} from 'rxjs';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {AgeRating} from 'src/app/_models/metadata/age-rating';
import {User} from 'src/app/_models/user/user';
import {AccountService} from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AgeRatingPipe} from '../../_pipes/age-rating.pipe';
import {RestrictionSelectorComponent} from '../restriction-selector/restriction-selector.component';
import {NgClass} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-change-age-restriction',
    templateUrl: './change-age-restriction.component.html',
    styleUrls: ['./change-age-restriction.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RestrictionSelectorComponent, AgeRatingPipe, TranslocoDirective,
        ReactiveFormsModule, SettingItemComponent, NgClass]
})
export class ChangeAgeRestrictionComponent implements OnInit {

  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);


  user: User | undefined = undefined;
  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  reset: EventEmitter<AgeRestriction> = new EventEmitter();

  canEdit = computed(() => {
    return this.accountService.hasChangeAgeRestrictionRole(this.accountService.currentUserSignal()!);
  });


  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), take(1)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.originalRestriction = this.user.ageRestriction;
      this.cdRef.markForCheck();
    });

    this.cdRef.markForCheck();
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  resetForm() {
    if (!this.user) return;
    this.reset.emit(this.originalRestriction);
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.user === undefined) { return; }

    this.accountService.updateAgeRestriction(this.selectedRestriction.ageRating, this.selectedRestriction.includeUnknowns).subscribe(() => {
      this.toastr.success(translate('toasts.age-restriction-updated'));
      this.originalRestriction = this.selectedRestriction;
      if (this.user) {
        this.user.ageRestriction.ageRating = this.selectedRestriction.ageRating;
        this.user.ageRestriction.includeUnknowns = this.selectedRestriction.includeUnknowns;
      }
      this.resetForm();

      this.cdRef.markForCheck();
    });
  }

  protected readonly AgeRating = AgeRating;

}
