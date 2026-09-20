import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {AgeRating} from "../../../_models/metadata/age-rating";
import {AgeRatingDto} from "../../../_models/metadata/age-rating-dto";
import {AgeRatingPipe} from "../../../_pipes/age-rating.pipe";
import {MetadataService} from "../../../_services/metadata.service";
import {ValidationErrorsComponent} from "../validation-errors/validation-errors.component";
import {applyEach, FieldTree, FormField, required, schema} from "@angular/forms/signals";
import {SettingSelectComponent} from "../../../settings/_components/setting-enum-select/setting-select.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

let nextId = 0;

/**
 * A row in the mapper. The API stores these as a Record, but a form needs a stable, ordered list.
 */
export interface AgeRatingMappingRow {
  str: string;
  rating: AgeRating;
}

export function toAgeRatingMappingRows(mappings: Record<string, AgeRating> | undefined | null): Array<AgeRatingMappingRow> {
  return Object.entries(mappings || {}).map(([str, rating]) => ({str, rating}));
}

export function packAgeRatingMappings(rows: Array<AgeRatingMappingRow>): Record<string, AgeRating> {
  return (rows || []).reduce((acc: Record<string, AgeRating>, row) => {
    const {str, rating} = row;
    if (str && rating) {
      acc[str] = rating;
    }
    return acc;
  }, {});
}

export const ageRatingMappingsSchema = schema<Array<AgeRatingMappingRow>>(p => {
  applyEach(p, row => {
    required(row.str);
    required(row.rating);
  });
});

/**
 * Reusable editor for a set of string -> {@link AgeRating} mappings.
 */
@Component({
  selector: 'app-age-rating-mapper',
  imports: [
    AgeRatingPipe,
    ValidationErrorsComponent,
    FormField,
    FormFieldDirective,
    SettingSelectComponent,
  ],
  templateUrl: './age-rating-mapper.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgeRatingMapperComponent implements OnInit {

  private readonly metadataService = inject(MetadataService);

  field = input.required<FieldTree<Array<AgeRatingMappingRow>>>();
  addLabel = input<string>('');
  removeLabel = input<string>('');
  sourceLabel = input<string>('');
  /** Prefix for generated DOM ids, so multiple mappers on a page don't collide */
  idPrefix = input<string>(`age-rating-mapper-${nextId++}`);
  /**
   * If any values present replaces the input field with a dropdown with these options
   */
  strValues = input<string[]>([]);

  ageRatings = signal<Array<AgeRatingDto>>([]);
  protected readonly ageRatingOptions = computed(() => this.ageRatings().map(r => r.value));

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });
  }

  addRow(str: string = '', rating: AgeRating = AgeRating.Unknown) {
    this.field()().value.update(rows => [...rows, {str, rating}]);
  }

  removeRow(index: number) {
    this.field()().value.update(rows => rows.filter((_, i) => i !== index));
  }

  protected readonly addText = computed(() => this.addLabel() || translate('common.add'));
  protected readonly removeText = computed(() => this.removeLabel() || translate('common.remove'));
}
