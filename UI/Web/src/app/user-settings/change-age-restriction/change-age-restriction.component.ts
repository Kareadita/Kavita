import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal
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
export class ChangeAgeRestrictionComponent {

  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);

  user = signal<User | undefined>(undefined);
  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  resetValue = signal<AgeRestriction | undefined>(undefined);

  canEdit = computed(() => {
    return this.accountService.hasChangeAgeRestrictionRole(this.accountService.currentUserSignal()!);
  });

  constructor() {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(), shareReplay(), take(1)).subscribe(user => {
      if (!user) return;
      this.user.set(user);
      this.originalRestriction = user.ageRestriction;
    });
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  resetForm() {
    if (!this.user()) return;
    this.resetValue.set({...this.originalRestriction});
  }

  saveForm() {
    if (this.user() === undefined) { return; }

    this.accountService.updateAgeRestriction(this.selectedRestriction.ageRating, this.selectedRestriction.includeUnknowns).subscribe(() => {
      this.toastr.success(translate('toasts.age-restriction-updated'));
      this.originalRestriction = this.selectedRestriction;
      const currentUser = this.user();
      if (currentUser) {
        currentUser.ageRestriction.ageRating = this.selectedRestriction.ageRating;
        currentUser.ageRestriction.includeUnknowns = this.selectedRestriction.includeUnknowns;
      }
      this.resetForm();
    });
  }

  protected readonly AgeRating = AgeRating;

}
