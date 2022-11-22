import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Observable, of, Subject, takeUntil, shareReplay, map, take } from 'rxjs';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-change-age-restriction',
  templateUrl: './change-age-restriction.component.html',
  styleUrls: ['./change-age-restriction.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeAgeRestrictionComponent implements OnInit {

  user: User | undefined = undefined;
  hasChangeAgeRestrictionAbility: Observable<boolean> = of(false);
  isViewMode: boolean = true;
  selectedRestriction!: AgeRestriction;
  originalRestriction!: AgeRestriction;
  reset: EventEmitter<AgeRestriction> = new EventEmitter();

  get AgeRating() { return AgeRating; } 

  private onDestroy = new Subject<void>();

  constructor(private accountService: AccountService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), shareReplay(), take(1)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.originalRestriction = this.user.ageRestriction;
      this.cdRef.markForCheck();
    });
    
    this.hasChangeAgeRestrictionAbility = this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), shareReplay(), map(user => {
      return user !== undefined && (!this.accountService.hasAdminRole(user) && this.accountService.hasChangeAgeRestrictionRole(user));
    }));
    this.cdRef.markForCheck();
  }

  updateRestrictionSelection(restriction: AgeRestriction) {
    this.selectedRestriction = restriction;
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
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
