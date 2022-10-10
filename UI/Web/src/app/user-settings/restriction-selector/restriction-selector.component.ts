import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Member } from 'src/app/_models/member';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { AgeRatingDto } from 'src/app/_models/metadata/age-rating-dto';
import { User } from 'src/app/_models/user';
import { MetadataService } from 'src/app/_services/metadata.service';

@Component({
  selector: 'app-restriction-selector',
  templateUrl: './restriction-selector.component.html',
  styleUrls: ['./restriction-selector.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RestrictionSelectorComponent implements OnInit, OnChanges {

  @Input() member: Member | undefined | User;
  @Input() isAdmin: boolean = false;
  /**
   * Show labels and description around the form
   */
  @Input() showContext: boolean = true;
  @Input() reset: EventEmitter<AgeRating> | undefined;
  @Output() selected: EventEmitter<AgeRating> = new EventEmitter<AgeRating>();
  

  ageRatings: Array<AgeRatingDto> = [];
  restrictionForm: FormGroup | undefined;

  constructor(private metadataService: MetadataService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {

    this.restrictionForm = new FormGroup({
      'ageRating': new FormControl(this.member?.ageRestriction || AgeRating.NotApplicable, [])
    });

    if (this.isAdmin) {
      this.restrictionForm.get('ageRating')?.disable();
    }

    if (this.reset) {
      this.reset.subscribe(e => {
        this.restrictionForm?.get('ageRating')?.setValue(e);
        this.cdRef.markForCheck();
      });
    }

    this.restrictionForm.get('ageRating')?.valueChanges.subscribe(e => {
      this.selected.emit(parseInt(e, 10));
    });

    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });


  }

  ngOnChanges() {
    if (!this.member) return;
    console.log('changes: ');
    this.restrictionForm?.get('ageRating')?.setValue(this.member?.ageRestriction || AgeRating.NotApplicable);
    this.cdRef.markForCheck();
  }

}
