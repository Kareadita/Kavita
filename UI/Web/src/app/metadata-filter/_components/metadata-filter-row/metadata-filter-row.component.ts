import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {FilterStatement} from '../../../_models/metadata/v2/filter-statement';
import {BehaviorSubject, distinctUntilChanged, filter, map, Observable, of, startWith, switchMap, tap} from 'rxjs';
import {MetadataService} from 'src/app/_services/metadata.service';
import {mangaFormatFilters} from 'src/app/_models/metadata/series-filter';
import {PersonRole} from 'src/app/_models/metadata/person';
import {LibraryService} from 'src/app/_services/library.service';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {FilterComparison} from 'src/app/_models/metadata/v2/filter-comparison';
import {allFields, FilterField} from 'src/app/_models/metadata/v2/filter-field';
import {AsyncPipe, NgForOf, NgIf, NgSwitch, NgSwitchCase, NgTemplateOutlet} from "@angular/common";
import {FilterFieldPipe} from "../../_pipes/filter-field.pipe";
import {FilterComparisonPipe} from "../../_pipes/filter-comparison.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {Select2Module, Select2Option} from "ng-select2-component";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";

enum PredicateType {
  Text = 1,
  Number = 2,
  Dropdown = 3,
}

const StringFields = [FilterField.SeriesName, FilterField.Summary, FilterField.Path, FilterField.FilePath];
const NumberFields = [FilterField.ReadTime, FilterField.ReleaseYear, FilterField.ReadProgress, FilterField.UserRating];
const DropdownFields = [FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating,
    FilterField.Translators, FilterField.Characters, FilterField.Publisher,
    FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
    FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
    FilterField.Writers, FilterField.Genres, FilterField.Libraries,
    FilterField.Formats, FilterField.CollectionTags, FilterField.Tags
];

const DropdownFieldsWithoutMustContains = [
  FilterField.Libraries, FilterField.Formats, FilterField.AgeRating, FilterField.PublicationStatus
];
const DropdownFieldsThatIncludeNumberComparisons = [
  FilterField.AgeRating
];
const NumberFieldsThatIncludeDateComparisons = [
  FilterField.ReleaseYear
];

const StringComparisons = [FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.BeginsWith,
  FilterComparison.EndsWith,
  FilterComparison.Matches];
const DateComparisons = [FilterComparison.IsBefore, FilterComparison.IsAfter, FilterComparison.IsInLast, FilterComparison.IsNotInLast];
const NumberComparisons = [FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.LessThan,
  FilterComparison.LessThanEqual,
  FilterComparison.GreaterThan,
  FilterComparison.GreaterThanEqual];
const DropdownComparisons = [FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.Contains,
  FilterComparison.NotContains,
  FilterComparison.MustContains];

@Component({
  selector: 'app-metadata-row-filter',
  templateUrl: './metadata-filter-row.component.html',
  styleUrls: ['./metadata-filter-row.component.scss'],
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AsyncPipe,
    FilterFieldPipe,
    FilterComparisonPipe,
    NgSwitch,
    NgSwitchCase,
    NgForOf,
    NgIf,
    Select2Module,
    NgTemplateOutlet,
    TagBadgeComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowComponent implements OnInit {

  @Input() index: number = 0; // This is only for debugging
  /**
   * Slightly misleading as this is the initial state and will be updated on the filterStatement event emitter
   */
  @Input() preset!: FilterStatement;
  @Input() availableFields: Array<FilterField> = allFields;
  @Output() filterStatement = new EventEmitter<FilterStatement>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
    'filterValue': new FormControl<string | number>('', []),
  });
  validComparisons$: BehaviorSubject<FilterComparison[]> = new BehaviorSubject([FilterComparison.Equal] as FilterComparison[]);
  predicateType$: BehaviorSubject<PredicateType> = new BehaviorSubject(PredicateType.Text as PredicateType);
  dropdownOptions$ = of<Select2Option[]>([]);

  loaded: boolean = false;
  protected readonly PredicateType = PredicateType;

  get MultipleDropdownAllowed() {
    const comp = parseInt(this.formGroup.get('comparison')?.value, 10) as FilterComparison;
    return comp === FilterComparison.Contains || comp === FilterComparison.NotContains || comp === FilterComparison.MustContains;
  }

  constructor(private readonly metadataService: MetadataService, private readonly libraryService: LibraryService,
    private readonly collectionTagService: CollectionTagService) {}

  ngOnInit() {
    console.log('[ngOnInit] creating stmt (' + this.index + '): ', this.preset)
    this.formGroup.addControl('input', new FormControl<FilterField>(FilterField.SeriesName, []));

    this.formGroup.get('input')?.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe((val: string) => this.handleFieldChange(val));
    this.populateFromPreset();

    this.formGroup.get('filterValue')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef), tap(v => console.log('filterValue: ', v))).subscribe();

    // Dropdown dynamic option selection
    this.dropdownOptions$ = this.formGroup.get('input')!.valueChanges.pipe(
      startWith(this.preset.value),
      distinctUntilChanged(),
      filter(() => {
        const inputVal = parseInt(this.formGroup.get('input')?.value, 10) as FilterField;
        return DropdownFields.includes(inputVal);
      }),
      switchMap((_) => this.getDropdownObservable()),
      takeUntilDestroyed(this.destroyRef)
    );


    this.formGroup!.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe(_ => {
      const stmt = {
        comparison: parseInt(this.formGroup.get('comparison')?.value, 10) as FilterComparison,
        field: parseInt(this.formGroup.get('input')?.value, 10) as FilterField,
        value: this.formGroup.get('filterValue')?.value!
      };

      // Some ids can get through and be numbers, convert them to strings for the backend
      if (typeof stmt.value === 'number' && !Number.isNaN(stmt.value)) {
        stmt.value = stmt.value + '';
      }

      console.log('Trying to update parent with new stmt: ', stmt.value);
      if (!stmt.value && stmt.field !== FilterField.SeriesName) return;
      console.log('updating parent with new statement: ', stmt.value)
      this.filterStatement.emit(stmt);
    });

    this.loaded = true;
    this.cdRef.markForCheck();
  }



  populateFromPreset() {
    const val = this.preset.value === "undefined" || !this.preset.value ? '' : this.preset.value;
    console.log('populating preset: ', val);
    this.formGroup.get('comparison')?.patchValue(this.preset.comparison);
    this.formGroup.get('input')?.patchValue(this.preset.field);

    if (StringFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(val);
    } else if (DropdownFields.includes(this.preset.field)) {
      if (this.MultipleDropdownAllowed || val.includes(',')) {
        console.log('setting multiple values: ', val.split(',').map(d => parseInt(d, 10)));
        this.formGroup.get('filterValue')?.patchValue(val.split(',').map(d => parseInt(d, 10)));
      } else {
        if (this.preset.field === FilterField.Languages) {
          this.formGroup.get('filterValue')?.patchValue(val);
        } else {
          this.formGroup.get('filterValue')?.patchValue(parseInt(val, 10));
        }
      }
    } else {
      this.formGroup.get('filterValue')?.patchValue(parseInt(val, 10));
    }


    this.cdRef.markForCheck();
  }

  getDropdownObservable(): Observable<Select2Option[]> {
      const filterField = parseInt(this.formGroup.get('input')?.value, 10) as FilterField;
      switch (filterField) {
        case FilterField.PublicationStatus:
          return this.metadataService.getAllPublicationStatus().pipe(map(pubs => pubs.map(pub => {
            return {value: pub.value, label: pub.title}
          })));
        case FilterField.AgeRating:
          return this.metadataService.getAllAgeRatings().pipe(map(ratings => ratings.map(rating => {
            return {value: rating.value, label: rating.title}
          })));
        case FilterField.Genres:
          return this.metadataService.getAllGenres().pipe(map(genres => genres.map(genre => {
            return {value: genre.id, label: genre.title}
          })));
        case FilterField.Languages:
          return this.metadataService.getAllLanguages().pipe(map(statuses => statuses.map(status => {
            return {value: status.isoCode, label: status.title + ` (${status.isoCode})`}
          })));
        case FilterField.Formats:
          return of(mangaFormatFilters).pipe(map(statuses => statuses.map(status => {
            return {value: status.value, label: status.title}
          })));
        case FilterField.Libraries:
          return this.libraryService.getLibraries().pipe(map(libs => libs.map(lib => {
            return {value: lib.id, label: lib.name}
          })));
        case FilterField.Tags:
          return this.metadataService.getAllTags().pipe(map(statuses => statuses.map(status => {
            return {value: status.id, label: status.title}
          })));
        case FilterField.CollectionTags:
          return this.collectionTagService.allTags().pipe(map(statuses => statuses.map(status => {
            return {value: status.id, label: status.title}
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
  }

  getPersonOptions(role: PersonRole) {
    return this.metadataService.getAllPeopleByRole(role).pipe(map(people => people.map(person => {
      return {value: person.id, label: person.name}
    })));
  }


  handleFieldChange(val: string) {
    const inputVal = parseInt(val, 10) as FilterField;
    console.log('HandleFieldChange: ', val);

    if (StringFields.includes(inputVal)) {
      this.validComparisons$.next(StringComparisons);
      this.predicateType$.next(PredicateType.Text);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue('');
        console.log('setting filterValue to empty string', this.formGroup.get('filterValue')?.value)
      }
      return;
    }

    if (NumberFields.includes(inputVal)) {
      const comps = [...NumberComparisons];
      if (NumberFieldsThatIncludeDateComparisons.includes(inputVal)) {
        comps.push(...DateComparisons);
      }
      this.validComparisons$.next(comps);
      this.predicateType$.next(PredicateType.Number);
      if (this.loaded) this.formGroup.get('filterValue')?.patchValue(0);
      return;
    }

    if (DropdownFields.includes(inputVal)) {
      let comps = [...DropdownComparisons];
      if (DropdownFieldsThatIncludeNumberComparisons.includes(inputVal)) {
        comps.push(...NumberComparisons);
      }
      if (DropdownFieldsWithoutMustContains.includes(inputVal)) {
        comps = comps.filter(c => c !== FilterComparison.MustContains);
      }
      this.validComparisons$.next(comps);
      this.predicateType$.next(PredicateType.Dropdown);
      return;
    }
  }

}
