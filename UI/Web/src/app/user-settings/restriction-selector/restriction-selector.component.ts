import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { AgeRestriction } from 'src/app/_models/metadata/age-restriction';
import { Member } from 'src/app/_models/auth/member';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { User } from 'src/app/_models/user';
import { MetadataService } from 'src/app/_services/metadata.service';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import {TitleCasePipe, NgTemplateOutlet} from '@angular/common';
import {TranslocoModule} from "@jsverse/transloco";

@Component({
    selector: 'app-restriction-selector',
    templateUrl: './restriction-selector.component.html',
    styleUrls: ['./restriction-selector.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [ReactiveFormsModule, NgbTooltip, TitleCasePipe, TranslocoModule, NgTemplateOutlet]
})
export class RestrictionSelectorComponent implements OnInit, OnChanges {

  @Input() member: Member | undefined | User;
  @Input() isAdmin: boolean = false;
  /**
   * Show labels and description around the form
   */
  @Input() showContext: boolean = true;
  @Input() reset: EventEmitter<AgeRestriction> | undefined;
  @Output() selected: EventEmitter<AgeRestriction> = new EventEmitter<AgeRestriction>();


  ageRatings: Array<AgeRatingDto> = [];
  restrictionForm: FormGroup | undefined;

  constructor(private metadataService: MetadataService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {

    this.restrictionForm = new FormGroup({
      'ageRating': new FormControl(this.member?.ageRestriction.ageRating || AgeRating.NotApplicable || AgeRating.NotApplicable, []),
      'ageRestrictionIncludeUnknowns': new FormControl(this.member?.ageRestriction.includeUnknowns || false, []),

    });

    if (this.isAdmin) {
      this.restrictionForm.get('ageRating')?.disable();
      this.restrictionForm.get('ageRestrictionIncludeUnknowns')?.disable();
    }

    if (this.reset) {
      this.reset.subscribe(e => {
        this.restrictionForm?.get('ageRating')?.setValue(e.ageRating);
        this.restrictionForm?.get('ageRestrictionIncludeUnknowns')?.setValue(e.includeUnknowns);
        this.cdRef.markForCheck();
      });
    }

    this.restrictionForm.get('ageRating')?.valueChanges.subscribe(e => {
      this.selected.emit({
        ageRating: parseInt(e, 10),
        includeUnknowns: this.restrictionForm?.get('ageRestrictionIncludeUnknowns')?.value
      });
      if (parseInt(e, 10) === AgeRating.NotApplicable) {
        this.restrictionForm!.get('ageRestrictionIncludeUnknowns')?.disable();
      } else {
        this.restrictionForm!.get('ageRestrictionIncludeUnknowns')?.enable();
      }
    });

    this.restrictionForm.get('ageRestrictionIncludeUnknowns')?.valueChanges.subscribe(e => {
      this.selected.emit({
        ageRating: parseInt(this.restrictionForm?.get('ageRating')?.value, 10),
        includeUnknowns: e
      });
    });

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });


  }

  ngOnChanges() {
    if (!this.member) return;
    this.restrictionForm?.get('ageRating')?.setValue(this.member?.ageRestriction.ageRating || AgeRating.NotApplicable);
    this.restrictionForm?.get('ageRestrictionIncludeUnknowns')?.setValue(this.member?.ageRestriction.includeUnknowns);
    this.cdRef.markForCheck();
  }

}
