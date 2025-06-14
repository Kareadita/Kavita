import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  Injector,
  input,
  Input,
  OnInit,
  Output,
  Signal,
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {FilterStatement} from '../../../_models/metadata/v2/filter-statement';
import {BehaviorSubject, distinctUntilChanged, filter, map, Observable, of, startWith, switchMap, tap} from 'rxjs';
import {MetadataService} from 'src/app/_services/metadata.service';
import {FilterComparison} from 'src/app/_models/metadata/v2/filter-comparison';
import {FilterField} from 'src/app/_models/metadata/v2/filter-field';
import {AsyncPipe} from "@angular/common";
import {FilterComparisonPipe} from "../../../_pipes/filter-comparison.pipe";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {Select2, Select2Option} from "ng-select2-component";
import {NgbDate, NgbDateParserFormatter, NgbInputDatepicker, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {ValidFilterEntity} from "../../filter-settings";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";

interface FieldConfig {
  type: PredicateType;
  baseComparisons: FilterComparison[];
  defaultValue: any;
  allowsDateComparisons?: boolean;
  allowsNumberComparisons?: boolean;
  excludesMustContains?: boolean;
  allowsIsEmpty?: boolean;
}

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

// const StringFields = [FilterField.SeriesName, FilterField.Summary, FilterField.Path, FilterField.FilePath, PersonFilterField.Name];
// const NumberFields = [
//   FilterField.ReadTime, FilterField.ReleaseYear, FilterField.ReadProgress,
//   FilterField.UserRating, FilterField.AverageRating, FilterField.ReadLast
// ];
// const DropdownFields = [
//   FilterField.PublicationStatus, FilterField.Languages, FilterField.AgeRating,
//   FilterField.Translators, FilterField.Characters, FilterField.Publisher,
//   FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
//   FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
//   FilterField.Writers, FilterField.Genres, FilterField.Libraries,
//   FilterField.Formats, FilterField.CollectionTags, FilterField.Tags,
//   FilterField.Imprint, FilterField.Team, FilterField.Location, PersonFilterField.Role
// ];
// const BooleanFields = [FilterField.WantToRead];
// const DateFields = [FilterField.ReadingDate];
//
// const DropdownFieldsWithoutMustContains = [
//   FilterField.Libraries, FilterField.Formats, FilterField.AgeRating, FilterField.PublicationStatus
// ];
// const DropdownFieldsThatIncludeNumberComparisons = [
//   FilterField.AgeRating
// ];
// const NumberFieldsThatIncludeDateComparisons = [
//   FilterField.ReleaseYear
// ];
//
// const FieldsThatShouldIncludeIsEmpty = [
//   FilterField.Summary, FilterField.UserRating, FilterField.Genres,
//   FilterField.CollectionTags, FilterField.Tags, FilterField.ReleaseYear,
//   FilterField.Translators, FilterField.Characters, FilterField.Publisher,
//   FilterField.Editor, FilterField.CoverArtist, FilterField.Letterer,
//   FilterField.Colorist, FilterField.Inker, FilterField.Penciller,
//   FilterField.Writers, FilterField.Imprint, FilterField.Team,
//   FilterField.Location,
// ];

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
  imports: [
    ReactiveFormsModule,
    AsyncPipe,
    FilterComparisonPipe,
    NgbTooltip,
    TranslocoDirective,
    NgbInputDatepicker,
    Select2
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowComponent<TFilter extends number = number, TSort extends number = number> implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dateParser = inject(NgbDateParserFormatter);
  private readonly metadataService = inject(MetadataService);
  private readonly translocoService = inject(TranslocoService);
  private readonly filterUtilitiesService = inject(FilterUtilitiesService);
  private readonly injector = inject(Injector);

  /**
   * Slightly misleading as this is the initial state and will be updated on the filterStatement event emitter
   */
  @Input() preset!: FilterStatement<TFilter>;
  entityType = input.required<ValidFilterEntity>();
  @Output() filterStatement = new EventEmitter<FilterStatement<TFilter>>();


  formGroup!: FormGroup;
  validComparisons$: BehaviorSubject<FilterComparison[]> = new BehaviorSubject([FilterComparison.Equal] as FilterComparison[]);
  predicateType$: BehaviorSubject<PredicateType> = new BehaviorSubject(PredicateType.Text as PredicateType);
  dropdownOptions$ = of<Select2Option[]>([]);

  loaded: boolean = false;


  private comparisonSignal!: Signal<FilterComparison>;
  private inputSignal!: Signal<TFilter>;

  isEmptySelected: Signal<boolean> = computed(() => false);
  uiLabel: Signal<FilterRowUi | null> = computed(() => null);
  isMultiSelectDropdownAllowed: Signal<boolean> = computed(() => false);

  filterFieldOptions: Signal<{title: string, value: TFilter}[]> = computed(() => []);

  ngOnInit() {

    this.formGroup = new FormGroup({
      'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
      'filterValue': new FormControl<string | number>('', []),
      'input': new FormControl<TFilter>(this.filterUtilitiesService.getDefaultFilterField<TFilter>(this.entityType()), [])
    });

    this.comparisonSignal = toSignal<FilterComparison>(
      this.formGroup.get('comparison')!.valueChanges.pipe(
        startWith(this.formGroup.get('comparison')!.value),
        map(d => parseInt(d + '', 10) as FilterComparison)
      )
      , {requireSync: true, injector: this.injector});
    this.inputSignal = toSignal<TFilter>(
      this.formGroup.get('input')!.valueChanges.pipe(
        startWith(this.formGroup.get('input')!.value),
        map(d => parseInt(d + '', 10) as TFilter)
      )
      , {requireSync: true, injector: this.injector});

    this.isEmptySelected = computed(() => this.comparisonSignal() !== FilterComparison.IsEmpty);
    this.uiLabel = computed(() => {
      if (!unitLabels.has(this.inputSignal())) return null;
      return unitLabels.get(this.inputSignal()) as FilterRowUi;
    });

    this.isMultiSelectDropdownAllowed = computed(() => {
      return this.comparisonSignal() === FilterComparison.Contains || this.comparisonSignal() === FilterComparison.NotContains || this.comparisonSignal() === FilterComparison.MustContains;
    });

    this.filterFieldOptions = computed(() => {
      return this.filterUtilitiesService.getFilterFields(this.entityType());
    });

    this.formGroup.get('input')?.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef)).subscribe((val: string) => this.handleFieldChange(val));
    this.populateFromPreset();

    this.formGroup.get('filterValue')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe();

    // Dropdown dynamic option selection
    this.dropdownOptions$ = this.formGroup.get('input')!.valueChanges.pipe(
      startWith(this.preset.value),
      distinctUntilChanged(),
      filter(() => {
        return this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType()).includes(this.inputSignal());
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
      field: parseInt(this.formGroup.get('input')?.value, 10) as TFilter,
      value: this.formGroup.get('filterValue')?.value!
    };

    const dateFields = this.filterUtilitiesService.getDateFields(this.entityType());
    const booleanFields = this.filterUtilitiesService.getBooleanFields(this.entityType());
    if (typeof stmt.value === 'object' && dateFields.includes(stmt.field)) {
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
      if (!stmt.value && (![FilterField.SeriesName, FilterField.Summary].includes(stmt.field) && !booleanFields.includes(stmt.field))) return;
    }

    this.filterStatement.emit(stmt);
  }

  populateFromPreset() {
    const val = this.preset.value === "undefined" || !this.preset.value ? '' : this.preset.value;
    this.formGroup.get('comparison')?.patchValue(this.preset.comparison);
    this.formGroup.get('input')?.patchValue(this.preset.field);

    const dropdownFields = this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType());
    const stringFields = this.filterUtilitiesService.getStringFields<TFilter>(this.entityType());
    const dateFields = this.filterUtilitiesService.getDateFields(this.entityType());
    const booleanFields = this.filterUtilitiesService.getBooleanFields(this.entityType());

    if (stringFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(val);
    } else if (booleanFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(val);
    } else if (dateFields.includes(this.preset.field)) {
      this.formGroup.get('filterValue')?.patchValue(this.dateParser.parse(val));
    }
    else if (dropdownFields.includes(this.preset.field)) {
      if (this.isMultiSelectDropdownAllowed() || val.includes(',')) {
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
      const filterField = this.inputSignal();
      return this.metadataService.getOptionsForFilterField<TFilter>(filterField, this.entityType());
  }

  handleFieldChange(val: string) {
    const inputVal = parseInt(val, 10) as TFilter;

    const stringFields = this.filterUtilitiesService.getStringFields<TFilter>(this.entityType());
    const dropdownFields = this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType());
    const numberFields = this.filterUtilitiesService.getNumberFields<TFilter>(this.entityType());
    const booleanFields = this.filterUtilitiesService.getBooleanFields<TFilter>(this.entityType());
    const dateFields = this.filterUtilitiesService.getDateFields<TFilter>(this.entityType());
    const fieldsThatShouldIncludeIsEmpty = this.filterUtilitiesService.getFieldsThatShouldIncludeIsEmpty<TFilter>(this.entityType());
    const numberFieldsThatIncludeDateComparisons = this.filterUtilitiesService.getNumberFieldsThatIncludeDateComparisons<TFilter>(this.entityType());
    const dropdownFieldsThatIncludeDateComparisons = this.filterUtilitiesService.getDropdownFieldsThatIncludeDateComparisons<TFilter>(this.entityType());
    const dropdownFieldsWithoutMustContains = this.filterUtilitiesService.getDropdownFieldsWithoutMustContains<TFilter>(this.entityType());
    const dropdownFieldsThatIncludeNumberComparisons = this.filterUtilitiesService.getDropdownFieldsThatIncludeNumberComparisons<TFilter>(this.entityType());

    if (stringFields.includes(inputVal)) {
      let comps = [...StringComparisons];

      if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Text);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue('');
        this.formGroup.get('comparison')?.patchValue(StringComparisons[0]);
      }
      this.cdRef.markForCheck();
      return;
    }

    if (numberFields.includes(inputVal)) {
      const comps = [...NumberComparisons];

      if (numberFieldsThatIncludeDateComparisons.includes(inputVal)) {
        comps.push(...DateComparisons);
      }
      if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Number);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(0);
        this.formGroup.get('comparison')?.patchValue(NumberComparisons[0]);
      }

      this.cdRef.markForCheck();
      return;
    }

    if (dateFields.includes(inputVal)) {
      const comps = [...DateComparisons];
      if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Date);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(false);
        this.formGroup.get('comparison')?.patchValue(DateComparisons[0]);
      }
      this.cdRef.markForCheck();
      return;
    }

    if (booleanFields.includes(inputVal)) {
      let comps = [...DateComparisons];
      if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }


      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Boolean);

      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(false);
        this.formGroup.get('comparison')?.patchValue(BooleanComparisons[0]);
      }
      this.cdRef.markForCheck();
      return;
    }

    if (dropdownFields.includes(inputVal)) {
      let comps = [...DropdownComparisons];
      if (dropdownFieldsThatIncludeNumberComparisons.includes(inputVal)) {
        comps.push(...NumberComparisons);
      }
      if (dropdownFieldsWithoutMustContains.includes(inputVal)) {
        comps = comps.filter(c => c !== FilterComparison.MustContains);
      }
      if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) {
        comps.push(FilterComparison.IsEmpty);
      }

      this.validComparisons$.next([...new Set(comps)]);
      this.predicateType$.next(PredicateType.Dropdown);
      if (this.loaded) {
        this.formGroup.get('filterValue')?.patchValue(0);
        this.formGroup.get('comparison')?.patchValue(comps[0]);
      }
      this.cdRef.markForCheck();
      return;
    }
  }

  onDateSelect(_: NgbDate) {
    this.propagateFilterUpdate();
  }

  updateIfDateFilled() {
    this.propagateFilterUpdate();
  }

  protected readonly FilterComparison = FilterComparison;
  protected readonly PredicateType = PredicateType;
}
