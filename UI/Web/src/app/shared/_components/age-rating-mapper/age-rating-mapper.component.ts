import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal
} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from "@angular/forms";
import {translate} from "@jsverse/transloco";
import {AgeRating} from "../../../_models/metadata/age-rating";
import {AgeRatingDto} from "../../../_models/metadata/age-rating-dto";
import {AgeRatingPipe} from "../../../_pipes/age-rating.pipe";
import {MetadataService} from "../../../_services/metadata.service";

let nextId = 0;

/**
 * A row in the mapper, as it lives in the FormArray
 */
export type AgeRatingMappingRow = {
  str?: string | null,
  rating?: AgeRating | null
};

export type AgeRatingMappingsArray = FormArray<FormGroup<{
  str: FormControl<string | null>,
  rating: FormControl<AgeRating | null>
}>>;

export function createAgeRatingMappingRow(fb: FormBuilder, str: string = '', rating: AgeRating = AgeRating.Unknown) {
  return fb.group({
    str: [str, Validators.required],
    rating: [rating, Validators.required]
  });
}

export function buildAgeRatingMappingsArray(fb: FormBuilder, mappings: Record<string, AgeRating> | undefined | null): AgeRatingMappingsArray {
  const array = fb.array<FormGroup<{
    str: FormControl<string | null>,
    rating: FormControl<AgeRating | null>
  }>>([]);

  Object.entries(mappings || {}).forEach(([str, rating]) => {
    array.push(createAgeRatingMappingRow(fb, str, rating));
  });

  return array;
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

/**
 * Reusable editor for a set of string -> {@link AgeRating} mappings.
 */
@Component({
  selector: 'app-age-rating-mapper',
  imports: [
    AgeRatingPipe,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './age-rating-mapper.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgeRatingMapperComponent implements OnInit {

  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);

  form = input.required<FormGroup>();
  controlName = input.required<string>();
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

  get mappingRows(): AgeRatingMappingsArray {
    return this.form().get(this.controlName()) as AgeRatingMappingsArray;
  }

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });
  }

  addRow(str: string = '', rating: AgeRating = AgeRating.Unknown) {
    this.mappingRows.push(createAgeRatingMappingRow(this.fb, str, rating));
    this.cdRef.markForCheck();
  }

  removeRow(index: number) {
    this.mappingRows.removeAt(index);
    this.cdRef.markForCheck();
  }

  protected readonly addText = computed(() => this.addLabel() || translate('common.add'));
  protected readonly removeText = computed(() => this.removeLabel() || translate('common.remove'));
}
