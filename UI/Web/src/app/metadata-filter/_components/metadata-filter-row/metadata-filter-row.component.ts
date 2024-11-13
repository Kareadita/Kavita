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
import {AsyncPipe, NgTemplateOutlet} from "@angular/common";
import {FilterFieldPipe} from "../../../_pipes/filter-field.pipe";
import {FilterComparisonPipe} from "../../../_pipes/filter-comparison.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {Select2Module, Select2Option} from "ng-select2-component";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {
  NgbDate,
  NgbDateParserFormatter,
  NgbDatepicker,
  NgbInputDatepicker,
  NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";

enum PredicateType {
  Text = 1,
  Number = 2,
  Dropdown = 3,
  Boolean = 4,
  Date = 5
}

class FilterRowUi {
  unit = '';
  tooltip = ''
  constructor(unit: string = '', tooltip: string = '') {
    this.unit = unit;
    this.tooltip = tooltip;
  }
}

const unitLabels: Map<FilterField, FilterRowUi> = new Map([
    [FilterField.ReadingDate, new FilterRowUi('unit-reading-date')],
    [FilterField.AverageRating, new FilterRowUi('unit-average-rating')],
    [FilterField.ReadProgress, new FilterRowUi('unit-reading-progress')],
    [FilterField.UserRating, new FilterRowUi('unit-user-rating')],
    [FilterField.ReadLast, new FilterRowUi('unit-read-last')],
]);

const StringFields = [FilterField.SeriesName, FilterField.Summary, FilterField.Path, FilterField.FilePath];
const NumberFields = [
  FilterField.ReadTime, FilterField.ReleaseYear, FilterField.ReadProgress,
  FilterField.UserRating, FilterField.AverageRating, FilterField.ReadLast
];
const DropdownFields = [
  FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating,
  FilterField.Translators, FilterField.Characters, FilterField.Publisher,
  FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
  FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
  FilterField.Writers, FilterField.Genres, FilterField.Libraries,
  FilterField.Formats, FilterField.CollectionTags, FilterField.Tags,
  FilterField.Imprint, FilterField.Team, FilterField.Location
];
const BooleanFields = [FilterField.WantToRead];
const DateFields = [FilterField.ReadingDate];

const DropdownFieldsWithoutMustContains = [
  FilterField.Libraries, FilterField.Formats, FilterField.AgeRating, FilterField.PublicationStatus
];
const DropdownFieldsThatIncludeNumberComparisons = [
  FilterField.AgeRating
];
const NumberFieldsThatIncludeDateComparisons = [
  FilterField.ReleaseYear
];

const FieldsThatShouldIncludeIsEmpty = [
  FilterField.Summary, FilterField.UserRating, FilterField.Genres,
  FilterField.CollectionTags, FilterField.Tags, FilterField.ReleaseYear,
  FilterField.Translators, FilterField.Characters, FilterField.Publisher,
  FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
  FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
  FilterField.Writers, FilterField.Imprint, FilterField.Team,
  FilterField.Location,
];

const StringComparisons = [
  FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.BeginsWith,
  FilterComparison.EndsWith,
  FilterComparison.Matches];
const DateComparisons = [
  FilterComparison.IsBefore,
  FilterComparison.IsAfter,
  FilterComparison.Equal,
  FilterComparison.NotEqual,];
const NumberComparisons = [
  FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.LessThan,
  FilterComparison.LessThanEqual,
  FilterComparison.GreaterThan,
  FilterComparison.GreaterThanEqual];
const DropdownComparisons = [
  FilterComparison.Equal,
  FilterComparison.NotEqual,
  FilterComparison.Contains,
  FilterComparison.NotContains,
  FilterComparison.MustContains];
const BooleanComparisons = [
  FilterComparison.Equal
]

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
    Select2Module,
    NgbTooltip,
    TranslocoDirective,
    NgbInputDatepicker
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
  private readonly dateParser = inject(NgbDateParserFormatter);

  protected readonly FilterComparison = FilterComparison;

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
    'filterValue': new FormControl<string | number>('', []),
  });
  validComparisons$: BehaviorSubject<FilterComparison[]> = new BehaviorSubject([FilterComparison.Equal] as FilterComparison[]);
  predicateType$: BehaviorSubject<PredicateType> = new BehaviorSubject(PredicateType.Text as PredicateType);
  dropdownOptions$ = of<Select2Option[]>([]);

  loaded: boolean = false;
  protected readonly PredicateType = PredicateType;

  get UiLabel(): FilterRowUi | null {
    const field = parseInt(this.formGroup.get('input')!.value, 10) as FilterField;
    if (!unitLabels.has(field)) return null;
    return unitLabels.get(field) as FilterRowUi;
  }

  get MultipleDropdownAllowed() {
    const comp = parseInt(this.formGroup.get('comparison')?.value, 10) as FilterComparison;
    return comp === FilterComparison.Contains || comp === FilterComparison.NotContains || comp === FilterComparison.MustContains;
  }

  constructor(private readonly metadataService: MetadataService, private readonly libraryService: LibraryService,
    private readonly collectionTagService: CollectionTagService) {}

  ngOnInit() {
    this.formGroup.addControl('input', new FormControl<FilterField>(FilterField.SeriesName, []));

    this.formGroup.get('input')?.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe((val: string) => this.handleFieldChange(val));
    this.populateFromPreset();

    this.formGroup.get('filterValue')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe();

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


    this.formGroup!.valueChanges.pipe(
      distinctUntilChanged(),
      tap(_ => this.propagateFilterUpdate()),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    this.loaded = true;
    this.cdRef.markForCheck();
  }

  propagateFilterUpdate() {
    const stmt = {
      comparison: parseInt(this.formGroup.get('comparison')?.value, 10) as FilterComparison,
      field: parseInt(this.formGroup.get('input')?.value, 10) as FilterField,
      value: this.formGroup.get('filterValue')?.value!
    };

    if (typeof stmt.value === 'object' && DateFields.includes(stmt.field)) {
      stmt.value = this.dateParser.format(stmt.value);
    }

    // Some ids can get through and be numbers, convert them to strings for the backend
    if (typeof stmt.value === 'number' && !Number.isNaN(stmt.value)) {
      stmt.value = stmt.value + '';
    }

    if (typeof stmt.value === 'boolean') {
      stmt.value = stmt.value + '';
    }

    if (stmt.comparison !== FilterComparison.IsEmpty) {
      if (!stmt.value && (![FilterField.SeriesName, FilterField.Summary].includes(stmt.field) && !BooleanFields.includes(stmt.field))) return;
    }

    this.filterStatement.emit(stmt);
  }

  populateFromPreset() {
    const val = this.preset.value === "undefined" || !this.preset.value ? '' : this.preset.value;
    this.formGroup.get('comparison')?.patchValue(this.preset.comparison);
    this.formGroup.get('input')?.patchValue(this.preset.field);

    if (StringFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(val);
    } else if (BooleanFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(val);
    } else if (DateFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(this.dateParser.parse(val));
    }
    else if (DropdownFields.includes(this.preset.field)) {
      if (this.MultipleDropdownAllowed || val.includes(',')) {
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
          return this.collectionTagService.allCollections().pipe(map(statuses => statuses.map(status => {
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
        case FilterField.Imprint: return this.getPersonOptions(PersonRole.Imprint);
        case FilterField.Team: return this.getPersonOptions(PersonRole.Team);
        case FilterField.Location: return this.getPersonOptions(PersonRole.Location);
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


    if (StringFields.includes(inputVal)) {
      let comps = [...StringComparisons];

      if (FieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Text);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue('');
        this.formGroup.get('comparison')?.patchValue(StringComparisons[0]);
      }
      return;
    }

    if (NumberFields.includes(inputVal)) {
      const comps = [...NumberComparisons];

      if (NumberFieldsThatIncludeDateComparisons.includes(inputVal)) {
        comps.push(...DateComparisons);
      }
      if (FieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Number);
      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(0);
        this.formGroup.get('comparison')?.patchValue(NumberComparisons[0]);
      }
      return;
    }

    if (DateFields.includes(inputVal)) {
      const comps = [...DateComparisons];
      if (FieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Date);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(false);
        this.formGroup.get('comparison')?.patchValue(DateComparisons[0]);
      }
      return;
    }

    if (BooleanFields.includes(inputVal)) {
      let comps = [...DateComparisons];
      if (FieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }


      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Boolean);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(false);
        this.formGroup.get('comparison')?.patchValue(BooleanComparisons[0]);
      }
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
      if (FieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Dropdown);
      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(0);
        this.formGroup.get('comparison')?.patchValue(comps[0]);
      }
      return;
    }
  }



  onDateSelect(_: NgbDate) {
    this.propagateFilterUpdate();
  }
  updateIfDateFilled() {
    this.propagateFilterUpdate();
  }
}
