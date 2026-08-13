import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, input, OnInit, signal} from '@angular/core';
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {MetadataFieldMapping, MetadataFieldType, MetadataSettings} from "../_models/metadata-settings";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AgeRating} from "../../_models/metadata/age-rating";
import {DownloadService} from "../../shared/_services/download.service";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {
  AgeRatingMapperComponent,
  AgeRatingMappingsArray,
  buildAgeRatingMappingsArray,
  packAgeRatingMappings
} from "../../shared/_components/age-rating-mapper/age-rating-mapper.component";

export type MetadataMappingsExport = {
  ageRatingMappings: Record<string, AgeRating>,
  fieldMappings: Array<MetadataFieldMapping>,
  blacklist: Array<string>,
  whitelist: Array<string>,
}

@Component({
  selector: 'app-manage-metadata-mappings',
  imports: [
    AgeRatingMapperComponent,
    FormsModule,
    ReactiveFormsModule,
    TranslocoDirective,
    NgbAccordionDirective,
    NgbAccordionItem,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionBody,
    LoadingComponent,
  ],
  templateUrl: './manage-metadata-mappings.component.html',
  styleUrl: './manage-metadata-mappings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMetadataMappingsComponent implements OnInit {

  private readonly downloadService = inject(DownloadService);
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

  /**
   * Sections start expanded, but collapse by default when they contain more than this many rows
   */
  private readonly collapseThreshold = 10;
  ageRatingCollapsed = signal(false);
  fieldMappingCollapsed = signal(false);
  isLoading = signal<boolean>(true);

  ageRatingMappings: AgeRatingMappingsArray = buildAgeRatingMappingsArray(this.fb, {});
  fieldMappings = this.fb.array<FormGroup<{
    id: FormControl<number | null>
    sourceType: FormControl<MetadataFieldType | null>,
    destinationType: FormControl<MetadataFieldType | null>,
    sourceValue: FormControl<string | null>,
    destinationValue: FormControl<string | null>,
    excludeFromSource: FormControl<boolean | null>,
  }>>([]);

  ngOnInit(): void {
    const settings = this.settings();
    const settingsForm = this.settingsForm();

    this.ageRatingMappings = buildAgeRatingMappingsArray(this.fb, settings.ageRatingMappings);
    settingsForm.addControl('ageRatingMappings', this.ageRatingMappings);
    settingsForm.addControl('fieldMappings', this.fieldMappings);

    if (settings.fieldMappings) {
      settings.fieldMappings.forEach(mapping => {
        this.addFieldMapping(mapping);
      });
    }

    this.ageRatingCollapsed.set(this.ageRatingMappings.length > this.collapseThreshold);
    this.fieldMappingCollapsed.set(this.fieldMappings.length > this.collapseThreshold);
    this.isLoading.set(false);
    this.cdRef.markForCheck();
  }

  public packData(): MetadataMappingsExport {
    const ageRatingMappings = packAgeRatingMappings(this.settingsForm().get('ageRatingMappings')?.value ?? []);

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
