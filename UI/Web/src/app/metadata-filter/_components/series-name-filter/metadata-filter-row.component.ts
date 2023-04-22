import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, inject, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FilterComparison } from '../../_models/filter-comparison';
import { FilterField, allFields } from '../../_models/filter-field';
import { FilterStatement } from '../../_models/filter-statement';
import { BehaviorSubject, filter, map, merge, of, switchMap, tap } from 'rxjs';
import { MetadataService } from 'src/app/_services/metadata.service';
import { mangaFormatFilters } from 'src/app/_models/metadata/series-filter';
import { PersonRole } from 'src/app/_models/metadata/person';
import { LibraryService } from 'src/app/_services/library.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';

enum PredicateType {
  Text = 1,
  Number = 2,
  Dropdown = 3,
}

@Component({
  selector: 'app-metadata-row-filter',
  templateUrl: './metadata-filter-row.component.html',
  styleUrls: ['./metadata-filter-row.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowComponent implements OnInit {

  @Input() preset!: FilterStatement;
  @Output() filterStatement = new EventEmitter<FilterStatement>();

  private readonly cdRef = inject(ChangeDetectorRef);

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
    'filterValue': new FormControl<string | number>('', []),
  });
  validComprisons$: BehaviorSubject<FilterComparison[]> = new BehaviorSubject([FilterComparison.Equal] as FilterComparison[]);
  predicateType$: BehaviorSubject<PredicateType> = new BehaviorSubject(PredicateType.Text as PredicateType);
  dropdownOptions$ = of<{value: number, title: string}[]>([]);
  allFields = allFields;
  

  get PredicateType() { return PredicateType };

  constructor(private readonly metadataService: MetadataService, private readonly libraryService: LibraryService, 
    private readonly collectionTagService: CollectionTagService) {}

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

        this.predicateType$.next(PredicateType.Text);
        this.formGroup.get('filterValue')?.setValue('');
      } 

      // Number based fields
      else if ([FilterField.ReadTime, FilterField.ReleaseYear, FilterField.ReadProgress, FilterField.UserRating].includes(inputVal)) {
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
        this.predicateType$.next(PredicateType.Number);
        this.formGroup.get('filterValue')?.setValue('');
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
        this.predicateType$.next(PredicateType.Dropdown);
      }
    });
    this.formGroup.get('input')?.setValue(this.preset.field);
    this.formGroup.get('filterValue')?.patchValue(this.preset.value);
    this.formGroup.get('comparison')?.patchValue(this.preset.comparison);

    // Dropdown dynamic option selection
    // TODO: takeUntil(this.onDestroy)
    this.dropdownOptions$ = this.formGroup.get('input')!.valueChanges.pipe(
      switchMap((vals) => {
        console.log('Dropdown recalc');
        const filterField = parseInt(this.formGroup.get('input')?.value, 10) as FilterField;
        switch (filterField) {
          case FilterField.PublicationStatus:
            return this.metadataService.getAllPublicationStatus().pipe(map(statuses => statuses.map(status => {
              return {value: status.value, title: status.title}
            })));
          case FilterField.AgeRating:
            return this.metadataService.getAllAgeRatings().pipe(map(statuses => statuses.map(status => {
              return {value: status.value, title: status.title}
            })));
          case FilterField.Genres:
            return this.metadataService.getAllGenres().pipe(map(statuses => statuses.map(status => {
              return {value: status.id, title: status.title}
            })));
          case FilterField.Languages:
            // TODO: Languages needs to be redesigned
            return of([{value: 0, title: 'This field needs a different DTO'}]);
            // return this.metadataService.getAllLanguages().pipe(map(statuses => statuses.map(status => {
            //   return {value: status.isoCode, title: status.title}
            // })));
          case FilterField.Formats:
            return of(mangaFormatFilters).pipe(map(statuses => statuses.map(status => {
              return {value: status.value, title: status.title}
            })));
          case FilterField.Libraries:
            return this.libraryService.getLibraries().pipe(map(statuses => statuses.map(status => {
              return {value: status.id, title: status.name}
            })));
          case FilterField.Tags:
            return this.metadataService.getAllTags().pipe(map(statuses => statuses.map(status => {
              return {value: status.id, title: status.title}
            })));
          case FilterField.CollectionTags:
            return this.collectionTagService.allTags().pipe(map(statuses => statuses.map(status => {
              return {value: status.id, title: status.title}
            })));
          case FilterField.Characters: return this.getPersonOptions(PersonRole.Character);
          case FilterField.Colorist: return this.getPersonOptions(PersonRole.Colorist);
          case FilterField.CoverArtist: return this.getPersonOptions(PersonRole.CoverArtist);
          case FilterField.Editor: return this.getPersonOptions(PersonRole.Editor);
          case FilterField.Inker: return this.getPersonOptions(PersonRole.Inker);
          case FilterField.Letterer: return this.getPersonOptions(PersonRole.Letterer);
          case FilterField.Penciller: return this.getPersonOptions(PersonRole.Penciller);
          case FilterField.Publisher: return this.getPersonOptions(PersonRole.Publisher);
          case FilterField.Translators: return this.getPersonOptions(PersonRole.Translator);
          case FilterField.Writers: return this.getPersonOptions(PersonRole.Writer);
        }
        return of([]);
      }),
      tap(opts => this.formGroup.get('filterValue')?.setValue(opts[0].value))
    );


    this.validComprisons$.subscribe(v => console.log('Valid Comparisons: ', v));
    this.predicateType$.subscribe(v => console.log('Predicate Type: ', v));
    this.dropdownOptions$.subscribe(options => console.log('Dropdown Options: ', options));

    

    this.formGroup.valueChanges.subscribe(_ => {
      console.log('Form change ');
      this.filterStatement.emit({
        comparison: this.formGroup.get('comparison')?.value!,
        field: parseInt(this.formGroup.get('input')?.value, 10) as FilterField,
        value: this.formGroup.get('filterValue')?.value!
      });
      
    });
  }

  getPersonOptions(role: PersonRole) {
    return this.metadataService.getAllPeople().pipe(map(statuses => statuses.filter(p2 => p2.role === role).map(status => {
      return {value: status.id, title: status.name}
    })))
  }

}
