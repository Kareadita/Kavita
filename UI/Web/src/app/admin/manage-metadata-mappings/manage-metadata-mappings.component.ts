import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, input, OnInit, signal} from '@angular/core';
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {MetadataFieldMapping, MetadataFieldType, MetadataSettings} from "../_models/metadata-settings";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {MetadataService} from "../../_services/metadata.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AgeRating} from "../../_models/metadata/age-rating";
import {DownloadService} from "../../shared/_services/download.service";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";

export type MetadataMappingsExport = {
  ageRatingMappings: Record<string, AgeRating>,
  fieldMappings: Array<MetadataFieldMapping>,
  blacklist: Array<string>,
  whitelist: Array<string>,
}

@Component({
  selector: 'app-manage-metadata-mappings',
  imports: [
    AgeRatingPipe,
    FormsModule,
    ReactiveFormsModule,
    TranslocoDirective,
    SettingMultiTextFieldComponent,
  ],
  templateUrl: './manage-metadata-mappings.component.html',
  styleUrl: './manage-metadata-mappings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMetadataMappingsComponent implements OnInit {

  private readonly downloadService = inject(DownloadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);


  /**
   * The FormGroup to use, this component will add its own controls
   */
  settingsForm = input.required<FormGroup>();
  settings = input.required<MetadataSettings>()
  /**
   * If we should display the extended metadata processing toggle and export button
   */
  showHeader = input(true);

  ageRatings = signal<Array<AgeRatingDto>>([]);

  ageRatingMappings = this.fb.array<FormGroup<{
    str: FormControl<string | null>,
    rating: FormControl<AgeRating | null>
  }>>([]);
  fieldMappings = this.fb.array<FormGroup<{
    id: FormControl<number | null>
    sourceType: FormControl<MetadataFieldType | null>,
    destinationType: FormControl<MetadataFieldType | null>,
    sourceValue: FormControl<string | null>,
    destinationValue: FormControl<string | null>,
    excludeFromSource: FormControl<boolean | null>,
  }>>([]);

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });

    const settings = this.settings();
    const settingsForm = this.settingsForm();

    settingsForm.addControl('blacklist', new FormControl(settings.blacklist, []));
    settingsForm.addControl('whitelist', new FormControl(settings.whitelist, []));
    settingsForm.addControl('ageRatingMappings', this.ageRatingMappings);
    settingsForm.addControl('fieldMappings', this.fieldMappings);

    if (settings.ageRatingMappings) {
      Object.entries(settings.ageRatingMappings).forEach(([str, rating]) => {
        this.addAgeRatingMapping(str, rating);
      });
    }

    if (settings.fieldMappings) {
      settings.fieldMappings.forEach(mapping => {
        this.addFieldMapping(mapping);
      });
    }

    this.cdRef.markForCheck();
  }

  public packData(): MetadataMappingsExport {
    const ageRatingMappings = this.ageRatingMappings.controls.reduce((acc: Record<string, AgeRating>, control) => {
      const { str, rating } = control.value;
      if (str && rating) {
        acc[str] = rating;
      }
      return acc;
    }, {});

    const fieldMappings = this.fieldMappings.controls
      .map((control) => control.value as MetadataFieldMapping)
      .filter(m => m.sourceValue.length > 0 && m.destinationValue.length > 0);
    return {
      ageRatingMappings: ageRatingMappings,
      fieldMappings: fieldMappings,
      blacklist: this.settingsForm().get('blacklist')?.value || [],
      whitelist: this.settingsForm().get('whitelist')?.value || [],
    }
  }

  export() {
    const data = this.packData();
    this.downloadService.downloadObjectAsJson(data, translate('manage-metadata-settings.export-file-name'));
  }

  addAgeRatingMapping(str: string = '', rating: AgeRating = AgeRating.Unknown) {
    const mappingGroup = this.fb.group({
      str: [str, Validators.required],
      rating: [rating, Validators.required]
    });

    this.ageRatingMappings.push(mappingGroup);
  }

  removeAgeRatingMappingRow(index: number) {
    this.ageRatingMappings.removeAt(index);
  }

  addFieldMapping(mapping: MetadataFieldMapping | null = null) {
    const mappingGroup = this.fb.group({
      id: [mapping?.id || 0],
      sourceType: [mapping?.sourceType || MetadataFieldType.Genre, Validators.required],
      destinationType: [mapping?.destinationType || MetadataFieldType.Genre, Validators.required],
      sourceValue: [mapping?.sourceValue || '', Validators.required],
      destinationValue: [mapping?.destinationValue || ''],
      excludeFromSource: [mapping?.excludeFromSource || false]
    });

    this.fieldMappings.push(mappingGroup);
  }

  removeFieldMappingRow(index: number) {
    this.fieldMappings.removeAt(index);
  }

  protected readonly MetadataFieldType = MetadataFieldType;
}
