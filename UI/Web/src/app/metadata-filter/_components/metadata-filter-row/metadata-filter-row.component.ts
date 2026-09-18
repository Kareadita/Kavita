import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  output,
  untracked
} from '@angular/core';
import {FilterStatement} from '../../../_models/metadata/v2/filter-statement';
import {NgStyle} from "@angular/common";
import {FilterComparisonPipe} from "../../../_pipes/filter-comparison.pipe";
import {rxResource} from "@angular/core/rxjs-interop";
import {Select2, Select2Option, Select2UpdateValue} from "ng-select2-component";
import {Observable} from "rxjs";
import {NgbDateParserFormatter, NgbDateStruct, NgbInputDatepicker, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ValidFilterEntity} from "../../filter-settings";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {AnnotationsFilterField} from "../../../_models/metadata/v2/annotations-filter";
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {MetadataService} from "../../../_services/metadata.service";
import {form, FormField, FormRoot, min} from "@angular/forms/signals";
import {SettingSelectComponent} from "../../../settings/_components/setting-enum-select/setting-select.component";
import {GenericFilterFieldPipe} from "../../../_pipes/generic-filter-field.pipe";

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

const unitLabels: Map<ValidFilterEntity, Map<number, FilterRowUi>> = new Map([
  ['series', new Map([
    [SeriesFilterField.ReadingDate as number, new FilterRowUi('unit-reading-date')],
    [SeriesFilterField.AverageRating as number, new FilterRowUi('unit-average-rating')],
    [SeriesFilterField.ReadProgress as number, new FilterRowUi('unit-reading-progress')],
    [SeriesFilterField.UserRating as number, new FilterRowUi('unit-user-rating')],
    [SeriesFilterField.ReadLast as number, new FilterRowUi('unit-read-last')],
    [SeriesFilterField.FileSize as number, new FilterRowUi('unit-file-size', 'disclaimer-file-size')]
  ])],
  ['annotation', new Map([
    [AnnotationsFilterField.HighlightSlots as number, new FilterRowUi('', 'disclaimer-highlight-slots')],
  ])],
])

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

interface FilterValues {
  textValue: string;
  numberValue: number | null;
  booleanValue: boolean;
  /** NgbDateStruct, or the raw string while typing an unparsable date */
  dateValue: NgbDateStruct | string | null;
  /** Ids, or the language code for Languages. Multi-select holds an array */
  dropdownValue: number | string | number[] | null;
}

/**
 * Each predicate type gets its own value so native controls bind to a correctly typed field. Only the one matching the
 * selected field's PredicateType is read.
 */
interface FormModel extends FilterValues {
  comparison: FilterComparison;
  input: number;
}

/**
 * Value a control resets to when the user picks a different field
 */
const defaultValues: FilterValues = {
  textValue: '',
  numberValue: 0,
  booleanValue: false,
  dateValue: null,
  dropdownValue: 0
};

interface FieldConfig {
  comparisons: FilterComparison[];
  predicateType: PredicateType;
}

const defaultFieldConfig: FieldConfig = {
  comparisons: [FilterComparison.Equal],
  predicateType: PredicateType.Text
};

const MultiSelectComparisons = [FilterComparison.Contains, FilterComparison.NotContains, FilterComparison.MustContains];

@Component({
  selector: 'app-metadata-row-filter',
  templateUrl: './metadata-filter-row.component.html',
  styleUrls: ['./metadata-filter-row.component.scss'],
  imports: [
    FilterComparisonPipe,
    NgbTooltip,
    TranslocoDirective,
    NgbInputDatepicker,
    Select2,
    NgStyle,
    FormRoot,
    FormField,
    SettingSelectComponent,
    GenericFilterFieldPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowComponent<TFilter extends number = number, TSort extends number = number> {

  private readonly dateParser = inject(NgbDateParserFormatter);
  private readonly metadataService = inject(MetadataService);
  private readonly filterUtilitiesService = inject(FilterUtilitiesService);

  /**
   * Initial state of the row. Changes are sent back up through filterStatement, which the parent applies in place
   */
  readonly preset = input.required<FilterStatement<TFilter>>();
  readonly entityType = input.required<ValidFilterEntity>();
  readonly filterStatement = output<FilterStatement<TFilter>>();

  private seededModel: FormModel | undefined;
  private readonly formModel = linkedSignal<FilterStatement<TFilter>, FormModel>({
    source: this.preset,
    computation: (preset) => {
      this.seededModel = this.toFormModel(preset);
      return this.seededModel;
    }
  });
  protected readonly formGroup = form(this.formModel, p => {
    min(p.numberValue, 0);
  });

  private readonly selectedField = computed(() => this.formModel().input as TFilter);
  private readonly selectedComparison = computed(() => this.formModel().comparison);

  /**
   * Comparisons and control type for the selected field. An unknown field keeps the last config
   */
  private readonly fieldConfig = linkedSignal<TFilter, FieldConfig>({
    source: this.selectedField,
    computation: (field, previous) => this.getFieldConfig(field) ?? previous?.value ?? defaultFieldConfig
  });
  protected readonly validComparisons = computed(() => [...new Set(this.fieldConfig().comparisons)]);
  protected readonly predicateType = computed(() => this.fieldConfig().predicateType);

  protected readonly isEmptySelected = computed(() => {
    const comparison = this.selectedComparison();
    return comparison === FilterComparison.IsEmpty || comparison === FilterComparison.IsNotEmpty;
  });
  protected readonly isMultiSelectDropdownAllowed = computed(() => MultiSelectComparisons.includes(this.selectedComparison()));
  protected readonly uiLabel = computed(() => unitLabels.get(this.entityType())?.get(this.selectedField()) ?? null);
  protected readonly filterFieldOptions = computed(() => this.filterUtilitiesService.getFilterFields<TFilter>(this.entityType()));

  // Dropdown dynamic option selection
  private readonly dropdownOptionsResource = rxResource({
    params: () => {
      const field = this.selectedField();
      return this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType()).includes(field) ? field : undefined;
    },
    stream: ({params: field}): Observable<Select2Option[]> => this.metadataService.getOptionsForFilterField<TFilter>(field, this.entityType())
  });
  protected readonly dropdownOptions = computed(() => {
    // Undefined until loaded, select2 only resolves the preset value against the data it's created with
    return this.dropdownOptionsResource.hasValue() ? this.dropdownOptionsResource.value() : undefined;
  });

  constructor() {
    // When the user picks a different field, reset the value and comparison to the new field's defaults
    effect(() => {
      const config = this.fieldConfig();
      untracked(() => {
        if (this.isSeededModel(this.formModel())) return;

        this.formModel.update(model => ({
          ...model,
          ...defaultValues,
          comparison: config.comparisons[0]
        }));
      });
    });

    effect(() => {
      const model = this.formModel();
      untracked(() => {
        if (this.isSeededModel(model)) return;

        this.propagateFilterUpdate(model);
      });
    });
  }

  /**
   * True when the model is untouched since it was seeded from the preset. Field writes always produce a new object.
   * Keeps a seed from resetting the preset's value, or echoing it back to the parent.
   */
  private isSeededModel(model: FormModel) {
    return model === this.seededModel;
  }

  /**
   * select2 isn't bound through [formField]: its CVA reports undefined whenever the value has no matching option
   * (options still loading, or the 0 default after a field change), and an undefined value orphans a signal form field.
   */
  protected updateDropdownValue(value: Select2UpdateValue) {
    const next = (value ?? null) as FilterValues['dropdownValue'];
    const current = this.formModel().dropdownValue;

    // select2 echoes a preselected multi-select back through update, don't treat that as a change
    const isSame = Array.isArray(next) && Array.isArray(current)
      ? next.length === current.length && next.every((v, i) => v === current[i])
      : next === current;
    if (isSame) return;

    this.formModel.update(model => ({...model, dropdownValue: next}));
  }

  private propagateFilterUpdate(model: FormModel) {
    const field = model.input as TFilter;
    const value = this.serializeValue(model);

    // A multi-select is sent even when emptied, so the statement doesn't keep the old selection
    const isMultiSelect = this.fieldConfig().predicateType === PredicateType.Dropdown && Array.isArray(model.dropdownValue);

    const booleanFields = this.filterUtilitiesService.getBooleanFields(this.entityType());
    if (model.comparison !== FilterComparison.IsEmpty) {
      if (!value && !isMultiSelect && (![SeriesFilterField.SeriesName, SeriesFilterField.Summary].includes(field) && !booleanFields.includes(field))) return;
    }

    this.filterStatement.emit({comparison: model.comparison, field, value});
  }

  /**
   * Reads the value for the selected field's predicate type, as the string the backend expects. Empty values become ''
   */
  private serializeValue(model: FormModel): string {
    switch (this.fieldConfig().predicateType) {
      case PredicateType.Text:
        return model.textValue;
      case PredicateType.Number:
        return this.serializeNumber(model.numberValue);
      case PredicateType.Boolean:
        return model.booleanValue + '';
      case PredicateType.Date:
        return typeof model.dateValue === 'string' ? model.dateValue : this.dateParser.format(model.dateValue);
      case PredicateType.Dropdown: {
        const value = model.dropdownValue;
        if (Array.isArray(value)) return value.join(',');
        return typeof value === 'string' ? value : this.serializeNumber(value);
      }
    }
  }

  private serializeNumber(value: number | null) {
    return value === null || Number.isNaN(value) ? '' : value + '';
  }

  private toFormModel(preset: FilterStatement<TFilter>): FormModel {
    const val = preset.value === "undefined" || !preset.value ? '' : preset.value;
    const model: FormModel = {...defaultValues, comparison: preset.comparison, input: preset.field};

    const dropdownFields = this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType());
    const stringFields = this.filterUtilitiesService.getStringFields<TFilter>(this.entityType());
    const dateFields = this.filterUtilitiesService.getDateFields(this.entityType());
    const booleanFields = this.filterUtilitiesService.getBooleanFields(this.entityType());

    if (stringFields.includes(preset.field)) {
      model.textValue = val;
    } else if (booleanFields.includes(preset.field)) {
      model.booleanValue = val === 'true';
    } else if (dateFields.includes(preset.field)) {
      model.dateValue = this.dateParser.parse(val);
    } else if (dropdownFields.includes(preset.field)) {
      if (MultiSelectComparisons.includes(preset.comparison) || val.includes(',')) {
        model.dropdownValue = val.split(',').map(d => parseInt(d, 10));
      } else if (preset.field === SeriesFilterField.Languages) {
        model.dropdownValue = val;
      } else {
        model.dropdownValue = parseInt(val, 10);
      }
    } else {
      const num = parseInt(val, 10);
      model.numberValue = Number.isNaN(num) ? null : num;
    }

    return model;
  }

  private getFieldConfig(inputVal: TFilter): FieldConfig | null {
    const stringFields = this.filterUtilitiesService.getStringFields<TFilter>(this.entityType());
    const dropdownFields = this.filterUtilitiesService.getDropdownFields<TFilter>(this.entityType());
    const numberFields = this.filterUtilitiesService.getNumberFields<TFilter>(this.entityType());
    const booleanFields = this.filterUtilitiesService.getBooleanFields<TFilter>(this.entityType());
    const dateFields = this.filterUtilitiesService.getDateFields<TFilter>(this.entityType());
    const fieldsThatShouldIncludeIsEmpty = this.filterUtilitiesService.getFieldsThatShouldIncludeIsEmpty<TFilter>(this.entityType());
    const fieldsThatShouldIncludeIsNotEmpty = this.filterUtilitiesService.getFieldsThatShouldIncludeIsNotEmpty<TFilter>(this.entityType());
    const numberFieldsThatIncludeDateComparisons = this.filterUtilitiesService.getNumberFieldsThatIncludeDateComparisons<TFilter>(this.entityType());
    const dropdownFieldsThatIncludeNumberComparisons = this.filterUtilitiesService.getDropdownFieldsThatIncludeNumberComparisons<TFilter>(this.entityType());
    const dropdownFieldsWithoutMustContains = this.filterUtilitiesService.getDropdownFieldsWithoutMustContains<TFilter>(this.entityType());
    const customComparisons = this.filterUtilitiesService.getCustomComparisons(this.entityType(), inputVal);

    let baseComparisons: FilterComparison[] = [];
    let predicateType: PredicateType;

    if (stringFields.includes(inputVal)) {
      baseComparisons = [...StringComparisons];
      predicateType = PredicateType.Text;
    } else if (numberFields.includes(inputVal)) {
      baseComparisons = [...NumberComparisons];
      if (numberFieldsThatIncludeDateComparisons.includes(inputVal)) {
        baseComparisons.push(...DateComparisons);
      }
      predicateType = PredicateType.Number;
    } else if (dateFields.includes(inputVal)) {
      baseComparisons = [...DateComparisons];
      predicateType = PredicateType.Date;
    } else if (booleanFields.includes(inputVal)) {
      baseComparisons = [...BooleanComparisons];
      predicateType = PredicateType.Boolean;
    } else if (dropdownFields.includes(inputVal)) {
      baseComparisons = [...DropdownComparisons];
      if (dropdownFieldsThatIncludeNumberComparisons.includes(inputVal)) {
        baseComparisons.push(...NumberComparisons);
      }
      if (dropdownFieldsWithoutMustContains.includes(inputVal)) {
        baseComparisons = baseComparisons.filter(c => c !== FilterComparison.MustContains);
      }
      predicateType = PredicateType.Dropdown;
    } else {
      return null;
    }

    if (fieldsThatShouldIncludeIsEmpty.includes(inputVal)) baseComparisons.push(FilterComparison.IsEmpty);
    if (fieldsThatShouldIncludeIsNotEmpty.includes(inputVal)) baseComparisons.push(FilterComparison.IsNotEmpty);

    // Custom comparisons need to also be included in some base type to drive the fields
    const comparisons = (customComparisons?.length ?? 0) > 0 ? customComparisons! : baseComparisons;

    return {comparisons, predicateType};
  }

  selectOptionStyle(c?: RgbaColor) {
    if (!c) return {}

    return { 'color': `rgba(${c.r}, ${c.g}, ${c.b}, ${c.a})` };
  }

  protected readonly PredicateType = PredicateType;
}
