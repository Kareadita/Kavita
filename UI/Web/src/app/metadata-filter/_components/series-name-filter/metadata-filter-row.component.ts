import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, inject, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FilterComparison } from '../../_models/filter-comparison';
import { FilterField, allFields } from '../../_models/filter-field';
import { FilterStatement } from '../../_models/filter-statement';
import { BehaviorSubject } from 'rxjs';

@Component({
  selector: 'app-metadata-row-filter',
  templateUrl: './metadata-filter-row.component.html',
  styleUrls: ['./metadata-filter-row.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowComponent implements OnInit {

  @Input() disabled: boolean = false;
  @Input() preset: FilterStatement | undefined;
  @Output() filterStatement = new EventEmitter<FilterStatement>();

  private readonly cdRef = inject(ChangeDetectorRef);

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
    'filterValue': new FormControl<string | number>('', []),
  });
  //tooltipHelp: string = 'Series name will filter against Name, Sort Name, or Localized Name';
  validComprisons = [
    FilterComparison.Equal,
    FilterComparison.NotEqual,
    FilterComparison.BeginsWith,
    FilterComparison.EndsWith,
    FilterComparison.Matches,
    FilterComparison.NotContains,
    FilterComparison.BeginsWith,
    FilterComparison.EndsWith,
    
  ];
  allFields = allFields;

  validComprisons$: BehaviorSubject<FilterComparison[]> = new BehaviorSubject([FilterComparison.Equal] as FilterComparison[]);
  


  get IsTextInput() {
    return [FilterField.SeriesName, FilterField.Summary].includes(this.formGroup.get('input')?.value!);
  }

  get IsNumberInput() {
    return [FilterField.ReadTime, FilterField.ReleaseYear, FilterField.AgeRating, FilterField.ReadProgress, FilterField.UserRating].includes(this.formGroup.get('input')?.value!);
  }

  // Multi-selection dropdown is also a thing
  get IsDropdown() {
    return [FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating, 
      FilterField.Translators, FilterField.Characters, FilterField.Publisher,
      FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
      FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
      FilterField.Writers, FilterField.Genres, FilterField.Libraries,
      FilterField.Formats,
    ].includes(this.formGroup.get('input')?.value!);
  }

  // We actually need the input type to be determined dynamically based on both field and comparison


  constructor() {}

  ngOnInit() {

    this.formGroup.addControl('input', new FormControl<FilterField>(FilterField.SeriesName, []));
    this.formGroup.get('input')?.valueChanges.subscribe((val: string) => {
      console.log('Input changed: ', val);
      const inputVal = parseInt(val, 10) as FilterField;
      if ([FilterField.SeriesName, FilterField.Summary].includes(inputVal)) {
        this.validComprisons$.next([FilterComparison.Equal,
          FilterComparison.NotEqual,
          FilterComparison.BeginsWith,
          FilterComparison.EndsWith,
          FilterComparison.Matches,
          FilterComparison.NotContains,
          FilterComparison.BeginsWith,
          FilterComparison.EndsWith]);
      } 
      
      // Number based fields
      else if ([FilterField.ReadTime, FilterField.ReleaseYear, FilterField.AgeRating, FilterField.ReadProgress, FilterField.UserRating].includes(inputVal)) {
        let comps = [FilterComparison.Equal,
          FilterComparison.NotEqual,
          FilterComparison.LessThan,
          FilterComparison.LessThanEqual,
          FilterComparison.GreaterThan,
          FilterComparison.GreaterThanEqual,];

        if (inputVal === FilterField.ReleaseYear) {
          comps.push(...[FilterComparison.IsBefore, FilterComparison.IsAfter, FilterComparison.IsInLast, FilterComparison.IsNotInLast]);
        }
        this.validComprisons$.next(comps);
      }

      // Multi-select dropdown fields
      else if ([FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating, 
        FilterField.Translators, FilterField.Characters, FilterField.Publisher,
        FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
        FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
        FilterField.Writers, FilterField.Genres, FilterField.Libraries,
        FilterField.Formats,
      ].includes(inputVal)) {
        this.validComprisons$.next([FilterComparison.Equal,
          FilterComparison.NotEqual,
          FilterComparison.Contains,
          FilterComparison.NotContains]);
      }

    });

    
    if (this.preset) {
      this.formGroup.get('input')?.setValue(this.preset.field);
    }

    this.validComprisons$.subscribe(v => console.log(v));

    

    this.formGroup.valueChanges.subscribe(_ => {
      this.filterStatement.emit({
        comparison: this.formGroup.get('comparison')?.value!,
        field: FilterField.SeriesName,
        value: this.formGroup.get('input')?.value!
      });
      
    });
  }

  remove() {

  }

}
