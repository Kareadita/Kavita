import {ChangeDetectorRef, Component, DestroyRef, inject, input, OnInit, signal} from '@angular/core';
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {MetadataFieldMapping, MetadataFieldType, MetadataSettings} from "../_models/metadata-settings";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {MetadataService} from "../../_services/metadata.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {AgeRating} from "../../_models/metadata/age-rating";

export type MetadataMappingsExport = {
  ageRatingMappings: Map<string, AgeRating>,
  fieldMappings: Array<MetadataFieldMapping>,
  blacklist: Array<string>,
  whitelist: Array<string>,
}

@Component({
  selector: 'app-manage-metadata-mappings',
  imports: [
    AgeRatingPipe,
    DefaultValuePipe,
    FormsModule,
    ReactiveFormsModule,
    SettingItemComponent,
    TagBadgeComponent,
    TranslocoDirective
  ],
  templateUrl: './manage-metadata-mappings.component.html',
  styleUrl: './manage-metadata-mappings.component.scss'
})
export class ManageMetadataMappingsComponent implements OnInit {

  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);

  settingsForm = input.required<FormGroup>();
  settings = input.required<MetadataSettings>()

  ageRatings = signal<Array<AgeRatingDto>>([]);
  ageRatingMappings = this.fb.array([]);
  fieldMappings = this.fb.array([]);

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings.set(ratings);
    });

    const settings = this.settings();
    const settingsForm = this.settingsForm();

    settingsForm.addControl('blacklist', new FormControl((settings.blacklist || '').join(','), []));
    settingsForm.addControl('whitelist', new FormControl((settings.whitelist || '').join(','), []));
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

  breakTags(csString: string) {
    if (csString) {
      return csString.split(',');
    }

    return [];
  }

  public packData(): MetadataMappingsExport {
    const ageRatingMappings = this.ageRatingMappings.controls.reduce((acc, control) => {
      // @ts-ignore
      const { str, rating } = control.value;
      if (str && rating) {
        // @ts-ignore
        acc[str] = parseInt(rating + '', 10) as AgeRating;
      }
      return acc;
    }, {});

    const fieldMappings = this.fieldMappings.controls.map((control) => {
      const value = control.value as MetadataFieldMapping;

      return {
        id: value.id,
        sourceType: parseInt(value.sourceType + '', 10),
        destinationType: parseInt(value.destinationType + '', 10),
        sourceValue: value.sourceValue,
        destinationValue: value.destinationValue,
        excludeFromSource: value.excludeFromSource
      }
    }).filter(m => m.sourceValue.length > 0 && m.destinationValue.length > 0);

    const blacklist = (this.settingsForm().get('blacklist')?.value || '').split(',').map((item: string) => item.trim()).filter((tag: string) => tag.length > 0);
    const whitelist = (this.settingsForm().get('whitelist')?.value || '').split(',').map((item: string) => item.trim()).filter((tag: string) => tag.length > 0);

    return {
      ageRatingMappings: ageRatingMappings as Map<string, AgeRating>,
      fieldMappings: fieldMappings,
      blacklist: blacklist,
      whitelist: whitelist,
    }
  }

  addAgeRatingMapping(str: string = '', rating: AgeRating = AgeRating.Unknown) {
    const mappingGroup = this.fb.group({
      str: [str, Validators.required],
      rating: [rating, Validators.required]
    });
    // @ts-ignore
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

    //@ts-ignore
    this.fieldMappings.push(mappingGroup);
  }

  removeFieldMappingRow(index: number) {
    this.fieldMappings.removeAt(index);
  }

  protected readonly MetadataFieldType = MetadataFieldType;
}
