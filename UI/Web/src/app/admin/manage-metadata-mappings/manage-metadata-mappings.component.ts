import {ChangeDetectionStrategy, Component, effect, inject, input, OnInit, signal} from '@angular/core';
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {MetadataFieldMapping, MetadataFieldType, MetadataSettings} from "../_models/metadata-settings";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AgeRating} from "../../_models/metadata/age-rating";
import {DownloadService} from "../../shared/_services/download.service";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {
  AgeRatingMapperComponent,
  AgeRatingMappingRow,
  ageRatingMappingsSchema,
  packAgeRatingMappings,
  toAgeRatingMappingRows
} from "../../shared/_components/age-rating-mapper/age-rating-mapper.component";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {TagWeightTitlePipe} from "../../_pipes/tag-weight-title.pipe";
import {allTagWeights, TagWeight} from "../_models/tag-weight.enum";
import {LicenseService} from "../../_services/license.service";
import {apply, applyEach, FieldTree, FormField, required, schema} from "@angular/forms/signals";
import {
  EnumOption,
  SettingSelectComponent
} from "../../settings/_components/setting-enum-select/setting-select.component";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";

export type MetadataMappingsExport = {
  ageRatingMappings: Record<string, AgeRating>,
  fieldMappings: Array<MetadataFieldMapping>,
  blacklist: Array<string>,
  whitelist: Array<string>,
}

/**
 * The slice of {@link MetadataSettings} this component edits. Parents nest it in their own form model and hand the
 * matching field tree over, so a parent's `valid()` covers these rows too.
 */
export interface MetadataMappingsFormModel {
  enableGenres: boolean;
  enableTags: boolean;
  filterAboveWeight: TagWeight | null;
  blacklist: Array<string>;
  whitelist: Array<string>;
  ageRatingMappings: Array<AgeRatingMappingRow>;
  externalAgeRatingMappings: Array<AgeRatingMappingRow>;
  fieldMappings: Array<MetadataFieldMapping>;
}

export const metadataMappingsSchema = schema<MetadataMappingsFormModel>(p => {
  apply(p.ageRatingMappings, ageRatingMappingsSchema);
  apply(p.externalAgeRatingMappings, ageRatingMappingsSchema);

  applyEach(p.fieldMappings, mapping => {
    required(mapping.sourceType);
    required(mapping.destinationType);
    required(mapping.sourceValue);
  });
});

export function toMetadataMappingsFormModel(settings: MetadataSettings): MetadataMappingsFormModel {
  return {
    enableGenres: settings.enableGenres,
    enableTags: settings.enableTags,
    filterAboveWeight: settings.filterAboveWeight,
    blacklist: settings.blacklist || [],
    whitelist: settings.whitelist || [],
    ageRatingMappings: toAgeRatingMappingRows(settings.ageRatingMappings),
    externalAgeRatingMappings: toAgeRatingMappingRows(settings.externalAgeRatingMappings),
    fieldMappings: [...(settings.fieldMappings || [])],
  };
}

/**
 * Rows a user started but never filled in are dropped rather than sent.
 */
export function packFieldMappings(mappings: Array<MetadataFieldMapping>): Array<MetadataFieldMapping> {
  return mappings.filter(m => m.sourceValue.length > 0 && m.destinationValue.length > 0);
}

/**
 * Everything this slice contributes to a {@link MetadataSettings} payload.
 */
export function packMetadataMappings(model: MetadataMappingsFormModel, withFieldMappings: boolean = true) {
  return {
    enableGenres: model.enableGenres,
    enableTags: model.enableTags,
    filterAboveWeight: model.filterAboveWeight,
    blacklist: model.blacklist,
    whitelist: model.whitelist,
    ageRatingMappings: packAgeRatingMappings(model.ageRatingMappings),
    externalAgeRatingMappings: packAgeRatingMappings(model.externalAgeRatingMappings),
    fieldMappings: withFieldMappings ? packFieldMappings(model.fieldMappings) : [],
  };
}

const MangaBakaAgeRatings = ['Safe', 'Suggestive', 'Erotica', 'Pornographic'];

@Component({
  selector: 'app-manage-metadata-mappings',
  imports: [
    AgeRatingMapperComponent,
    TranslocoDirective,
    NgbAccordionDirective,
    NgbAccordionItem,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionBody,
    LoadingComponent,
    FormFieldDirective,
    ValidationErrorsComponent,
    SettingItemComponent,
    SettingSwitchComponent,
    TagWeightTitlePipe,
    FormField,
    SettingSelectComponent,
    SettingMultiTextFieldComponent,
  ],
  templateUrl: './manage-metadata-mappings.component.html',
  styleUrl: './manage-metadata-mappings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMetadataMappingsComponent implements OnInit {

  protected readonly licenseService = inject(LicenseService);
  private readonly downloadService = inject(DownloadService);

  /**
   * The field tree for this slice. The parent owns the model and seeds it.
   */
  field = input.required<FieldTree<MetadataMappingsFormModel>>();
  /**
   * Genres/Tags are written by the parent's own settings on some pages, so they can be hidden here
   */
  showGenreTagToggles = input(true);

  /**
   * Sections start expanded, but collapse by default when they contain more than this many rows
   */
  private readonly collapseThreshold = 10;
  ageRatingCollapsed = signal(false);
  fieldMappingCollapsed = signal(false);
  isLoading = signal<boolean>(true);

  protected readonly allTagWeightOptions: Array<EnumOption<TagWeight>> = allTagWeights.map(w => ({value: w}));

  constructor() {
    // Collapse reflects what was loaded, not the live row count, so adding a row can't snap the panel shut
    let seeded = false;
    effect(() => {
      const model = this.field()().value();
      if (seeded) return;

      seeded = true;
      this.ageRatingCollapsed.set(model.ageRatingMappings.length > this.collapseThreshold);
      this.fieldMappingCollapsed.set(model.fieldMappings.length > this.collapseThreshold);
    });
  }

  ngOnInit(): void {
    this.isLoading.set(false);
  }

  public packData(): MetadataMappingsExport {
    const model = this.field()().value();

    return {
      ageRatingMappings: packAgeRatingMappings(model.ageRatingMappings),
      fieldMappings: packFieldMappings(model.fieldMappings),
      blacklist: model.blacklist,
      whitelist: model.whitelist,
    }
  }

  export() {
    const data = this.packData();
    this.downloadService.downloadObjectAsJson(data, translate('manage-metadata-settings.export-file-name'));
  }

  addFieldMapping(mapping: MetadataFieldMapping | null = null) {
    this.field().fieldMappings().value.update(mappings => [...mappings, {
      id: mapping?.id || 0,
      sourceType: mapping?.sourceType || MetadataFieldType.Genre,
      destinationType: mapping?.destinationType || MetadataFieldType.Genre,
      sourceValue: mapping?.sourceValue || '',
      destinationValue: mapping?.destinationValue || '',
      excludeFromSource: mapping?.excludeFromSource || false,
    }]);
  }

  removeFieldMappingRow(index: number) {
    this.field().fieldMappings().value.update(mappings => mappings.filter((_, i) => i !== index));
  }

  protected readonly MetadataFieldType = MetadataFieldType;
  protected readonly metadataFieldTypeOptions: Array<EnumOption<MetadataFieldType>> = [
    {value: MetadataFieldType.Genre},
    {value: MetadataFieldType.Tag},
  ];
  protected readonly MangaBakaAgeRatings = MangaBakaAgeRatings;
}
