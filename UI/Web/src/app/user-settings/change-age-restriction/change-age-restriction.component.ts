import {ChangeDetectionStrategy, Component, effect, inject, signal} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {AgeRating} from 'src/app/_models/metadata/age-rating';
import {AccountService} from 'src/app/_services/account.service';
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

  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  resetValue = signal<AgeRestriction | undefined>(undefined);
  canEdit = this.accountService.hasChangeAgeRestrictionRole;

  constructor() {
    effect(() => {
      const user = this.accountService.currentUser();
      if (!user) return;
      this.originalRestriction = user.ageRestriction;
    });
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  resetForm() {
    if (!this.accountService.currentUser()) return;
    this.resetValue.set({...this.originalRestriction});
  }

  saveForm() {
    if (this.accountService.currentUser() === undefined) { return; }

    this.accountService.updateAgeRestriction(this.selectedRestriction.ageRating, this.selectedRestriction.includeUnknowns).subscribe(() => {
      this.toastr.success(translate('toasts.age-restriction-updated'));
      this.originalRestriction = this.selectedRestriction;

      const currentUser = this.accountService.currentUser();
      if (currentUser) {
        currentUser.ageRestriction.ageRating = this.selectedRestriction.ageRating;
        currentUser.ageRestriction.includeUnknowns = this.selectedRestriction.includeUnknowns;
      }
      this.resetForm();
    });
  }

  protected readonly AgeRating = AgeRating;

}
