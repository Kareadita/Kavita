import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {SettingsService} from "../settings.service";
import {debounceTime, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, map} from "rxjs/operators";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {AgeRating} from "../../_models/metadata/age-rating";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {MetadataFieldMapping, MetadataFieldType} from "../_models/metadata-settings";
import {PersonRole} from "../../_models/metadata/person";
import {PersonRolePipe} from "../../_pipes/person-role.pipe";
import {allMetadataSettingField, MetadataSettingField} from "../_models/metadata-setting-field";
import {MetadataSettingFiledPipe} from "../../_pipes/metadata-setting-filed.pipe";


@Component({
  selector: 'app-manage-metadata-settings',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingSwitchComponent,
    SettingItemComponent,
    DefaultValuePipe,
    TagBadgeComponent,
    AgeRatingPipe,
    PersonRolePipe,
    MetadataSettingFiledPipe,
  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  protected readonly MetadataFieldType = MetadataFieldType;

  private readonly settingService = inject(SettingsService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  settingsForm: FormGroup = new FormGroup({});
  ageRatings: Array<AgeRatingDto> = [];
  ageRatingMappings = this.fb.array([]);
  fieldMappings = this.fb.array([]);
  personRoles: PersonRole[] = [PersonRole.Writer, PersonRole.CoverArtist, PersonRole.Character];
  isLoaded = false;
  allMetadataSettingFields = allMetadataSettingField;

  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });


    this.settingsForm.addControl('ageRatingMappings', this.ageRatingMappings);
    this.settingsForm.addControl('fieldMappings', this.fieldMappings);

    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settingsForm.addControl('enabled', new FormControl(settings.enabled, []));
      this.settingsForm.addControl('enableSummary', new FormControl(settings.enableSummary, []));
      this.settingsForm.addControl('enableLocalizedName', new FormControl(settings.enableLocalizedName, []));
      this.settingsForm.addControl('enablePublicationStatus', new FormControl(settings.enablePublicationStatus, []));
      this.settingsForm.addControl('enableRelations', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enableGenres', new FormControl(settings.enableGenres, []));
      this.settingsForm.addControl('enableTags', new FormControl(settings.enableTags, []));
      this.settingsForm.addControl('enableRelationships', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enablePeople', new FormControl(settings.enablePeople, []));
      this.settingsForm.addControl('enableStartDate', new FormControl(settings.enableStartDate, []));
      this.settingsForm.addControl('enableCoverImage', new FormControl(settings.enableCoverImage, []));

      this.settingsForm.addControl('blacklist', new FormControl((settings.blacklist || '').join(','), []));
      this.settingsForm.addControl('whitelist', new FormControl((settings.whitelist || '').join(','), []));
      this.settingsForm.addControl('firstLastPeopleNaming', new FormControl((settings.firstLastPeopleNaming), []));
      this.settingsForm.addControl('personRoles', this.fb.group(
        Object.fromEntries(
          this.personRoles.map((role, index) => [
            `personRole_${index}`,
            this.fb.control((settings.personRoles || this.personRoles).includes(role)),
          ])
        )
      ));

      this.settingsForm.addControl('overrides', this.fb.group(
        Object.fromEntries(
          this.allMetadataSettingFields.map((role: MetadataSettingField, index: number) => [
            `override_${index}`,
            this.fb.control((settings.overrides || []).includes(role)),
          ])
        )
      ));


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

      this.settingsForm.get('enablePeople')?.valueChanges.subscribe(enabled => {
        const firstLastControl = this.settingsForm.get('firstLastPeopleNaming');
        if (enabled) {
          firstLastControl?.enable();
        } else {
          firstLastControl?.disable();
        }
      });

      this.settingsForm.get('enablePeople')?.updateValueAndValidity();

      // Disable personRoles checkboxes based on enablePeople state
      this.settingsForm.get('enablePeople')?.valueChanges.subscribe(enabled => {
        const personRolesArray = this.settingsForm.get('personRoles') as FormArray;
        if (enabled) {
          personRolesArray.enable();
        } else {
          personRolesArray.disable();
        }
      });

      this.isLoaded = true;
      this.cdRef.markForCheck();


      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        takeUntilDestroyed(this.destroyRef),
        map(_ => this.packData()),
        switchMap((data) => this.settingService.updateMetadataSettings(data)),
      ).subscribe();

    });

  }

  breakTags(csString: string) {
    if (csString) {
      return csString.split(',');
    }

    return [];
  }


  packData(withFieldMappings: boolean = true) {
    const model = this.settingsForm.value;

    // Convert FormArray to dictionary
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
    }).filter(m => m.sourceValue.length > 0);

    // Translate blacklist string -> Array<string>
    return {
      ...model,
      ageRatingMappings,
      fieldMappings: withFieldMappings ? fieldMappings : [],
      blacklist: (model.blacklist || '').split(',').map((item: string) => item.trim()).filter((tag: string) => tag.length > 0),
      whitelist: (model.whitelist || '').split(',').map((item: string) => item.trim()).filter((tag: string) => tag.length > 0),
      personRoles: Object.entries(this.settingsForm.get('personRoles')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.personRoles[parseInt(key.split('_')[1], 10)]),
      overrides: Object.entries(this.settingsForm.get('overrides')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.allMetadataSettingFields[parseInt(key.split('_')[1], 10)])
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

    // Autofill destination value if empty when source value loses focus
    mappingGroup.get('sourceValue')?.valueChanges
      .pipe(
        filter(() => !mappingGroup.get('destinationValue')?.value)
      )
      .subscribe(sourceValue => {
        mappingGroup.get('destinationValue')?.setValue(sourceValue);
      });

    //@ts-ignore
    this.fieldMappings.push(mappingGroup);
  }

  removeFieldMappingRow(index: number) {
    this.fieldMappings.removeAt(index);
  }


}
