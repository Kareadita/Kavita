import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {AgeRestriction} from 'src/app/_models/metadata/age-restriction';
import {Member} from 'src/app/_models/auth/member';
import {AgeRating} from 'src/app/_models/metadata/age-rating';
import {AgeRatingDto} from 'src/app/_models/metadata/age-rating-dto';
import {User} from 'src/app/_models/user/user';
import {MetadataService} from 'src/app/_services/metadata.service';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {TranslocoModule} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
    selector: 'app-restriction-selector',
    templateUrl: './restriction-selector.component.html',
    styleUrls: ['./restriction-selector.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, NgbTooltip, TitleCasePipe, TranslocoModule, NgTemplateOutlet]
})
export class RestrictionSelectorComponent {
  private readonly metadataService = inject(MetadataService);

  // Inputs/Outputs
  member = input<Member | User | undefined>();
  isAdmin = input(false);
  showContext = input(true);
  resetValue = input<AgeRestriction | undefined>();
  selected = output<AgeRestriction>();

  // State
  ageRatings = signal<AgeRatingDto[]>([]);
  restrictionForm: FormGroup;

  constructor() {
    this.restrictionForm = new FormGroup({
      'ageRating': new FormControl(AgeRating.NotApplicable, []),
      'ageRestrictionIncludeUnknowns': new FormControl(false, []),
    });

    // React to member changes — patch form values
    effect(() => {
      const m = this.member();
      if (!m) return;
      this.restrictionForm.get('ageRating')!.setValue(m.ageRestriction.ageRating || AgeRating.NotApplicable);
      this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.setValue(m.ageRestriction.includeUnknowns);
    });

    // React to resetValue changes
    effect(() => {
      const r = this.resetValue();
      if (r == null) return;
      this.restrictionForm.get('ageRating')!.setValue(r.ageRating);
      this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.setValue(r.includeUnknowns);
    });

    // React to isAdmin changes — enable/disable form controls
    effect(() => {
      if (this.isAdmin()) {
        this.restrictionForm.get('ageRating')!.disable();
        this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.disable();
      } else {
        this.restrictionForm.get('ageRating')!.enable();
        // Re-check if includeUnknowns should stay disabled based on current rating
        const currentRating = parseInt(this.restrictionForm.get('ageRating')!.value, 10);
        if (currentRating === AgeRating.NotApplicable) {
          this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.disable();
        } else {
          this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.enable();
        }
      }
    });

    // Load age ratings
    this.metadataService.getAllAgeRatings()
      .pipe(takeUntilDestroyed())
      .subscribe(ratings => this.ageRatings.set(ratings));

    // Wire up valueChanges → output
    this.restrictionForm.get('ageRating')!.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(e => {
        this.selected.emit({
          ageRating: parseInt(e, 10),
          includeUnknowns: this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.value
        });
        if (parseInt(e, 10) === AgeRating.NotApplicable) {
          this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.disable();
        } else {
          this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.enable();
        }
      });

    this.restrictionForm.get('ageRestrictionIncludeUnknowns')!.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(e => {
        this.selected.emit({
          ageRating: parseInt(this.restrictionForm.get('ageRating')!.value, 10),
          includeUnknowns: e
        });
      });
  }
}
