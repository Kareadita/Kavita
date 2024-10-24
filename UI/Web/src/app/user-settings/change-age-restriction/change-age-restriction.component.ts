import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, of, shareReplay, map, take } from 'rxjs';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { AgeRatingPipe } from '../../_pipes/age-rating.pipe';
import { RestrictionSelectorComponent } from '../restriction-selector/restriction-selector.component';
import { NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import {AsyncPipe, NgClass, NgForOf, NgIf} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-change-age-restriction',
    templateUrl: './change-age-restriction.component.html',
    styleUrls: ['./change-age-restriction.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbCollapse, RestrictionSelectorComponent, AsyncPipe, AgeRatingPipe, TranslocoDirective, SettingTitleComponent,
    ReactiveFormsModule, SettingItemComponent, NgClass]
})
export class ChangeAgeRestrictionComponent implements OnInit {

  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly AgeRating = AgeRating;

  user: User | undefined = undefined;
  hasChangeAgeRestrictionAbility: Observable<boolean> = of(false);
  isViewMode: boolean = true;
  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  reset: EventEmitter<AgeRestriction> = new EventEmitter();


  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), take(1)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.originalRestriction = this.user.ageRestriction;
      this.cdRef.markForCheck();
    });

    this.hasChangeAgeRestrictionAbility = this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), map(user => {
      return user !== undefined && !this.accountService.hasReadOnlyRole(user) && (!this.accountService.hasAdminRole(user) && this.accountService.hasChangeAgeRestrictionRole(user));
    }));
    this.cdRef.markForCheck();
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;

    this.saveForm();
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
      this.isViewMode = true;
      this.cdRef.markForCheck();
    }, err => {

    });
  }

  updateEditMode(mode: boolean) {
    this.isViewMode = !mode;
    this.resetForm();
    this.cdRef.markForCheck();
  }

}
