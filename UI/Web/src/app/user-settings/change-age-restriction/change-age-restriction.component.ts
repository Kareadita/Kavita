import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, of, Subject, takeUntil, shareReplay, map, take } from 'rxjs';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { AgeRatingPipe } from '../../pipe/age-rating.pipe';
import { RestrictionSelectorComponent } from '../restriction-selector/restriction-selector.component';
import { NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import { NgIf, AsyncPipe } from '@angular/common';

@Component({
    selector: 'app-change-age-restriction',
    templateUrl: './change-age-restriction.component.html',
    styleUrls: ['./change-age-restriction.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgIf, NgbCollapse, RestrictionSelectorComponent, AsyncPipe, AgeRatingPipe]
})
export class ChangeAgeRestrictionComponent implements OnInit {

  user: User | undefined = undefined;
  hasChangeAgeRestrictionAbility: Observable<boolean> = of(false);
  isViewMode: boolean = true;
  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  reset: EventEmitter<AgeRestriction> = new EventEmitter();
  private readonly destroyRef = inject(DestroyRef);

  get AgeRating() { return AgeRating; }

  constructor(private accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), take(1)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.originalRestriction = this.user.ageRestriction;
      this.cdRef.markForCheck();
    });

    this.hasChangeAgeRestrictionAbility = this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), shareReplay(), map(user => {
      return user !== undefined && (!this.accountService.hasAdminRole(user) && this.accountService.hasChangeAgeRestrictionRole(user));
    }));
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
      this.toastr.success('Age Restriction has been updated');
      this.originalRestriction = this.selectedRestriction;
      if (this.user) {
        this.user.ageRestriction.ageRating = this.selectedRestriction.ageRating;
        this.user.ageRestriction.includeUnknowns = this.selectedRestriction.includeUnknowns;
      }
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
