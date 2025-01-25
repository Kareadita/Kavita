import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {SettingsService} from "../settings.service";
import {debounceTime, switchMap, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {AgeRating} from "../../_models/metadata/age-rating";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";


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
    AgeRatingPipe
  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  private readonly settingService = inject(SettingsService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  settingsForm: FormGroup = new FormGroup({});
  ageRatings: Array<AgeRatingDto> = [];
  ageRatingMappings = this.fb.array([]);


  ngOnInit(): void {
    this.metadataService.getAllAgeRatings().subscribe(ratings => {
      this.ageRatings = ratings;
      this.cdRef.markForCheck();
    });


    this.settingsForm.addControl('ageRatingMappings', this.ageRatingMappings);
    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settingsForm.addControl('enableSummary', new FormControl(settings.enableSummary, []));
      this.settingsForm.addControl('enablePublicationStatus', new FormControl(settings.enablePublicationStatus, []));
      this.settingsForm.addControl('enableRelations', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enableGenres', new FormControl(settings.enableGenres, []));
      this.settingsForm.addControl('enableTags', new FormControl(settings.enableTags, []));
      this.settingsForm.addControl('enableRelationships', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enablePeople', new FormControl(settings.enablePeople, []));
      this.settingsForm.addControl('enableStartDate', new FormControl(settings.enableStartDate, []));

      this.settingsForm.addControl('blacklist', new FormControl((settings.blacklist || '').join(','), []));
      if (settings.ageRatingMappings) {
        Object.entries(settings.ageRatingMappings).forEach(([str, rating]) => {
          this.addAgeRatingMapping(str, rating);
        });
      }

      this.cdRef.markForCheck();


      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        takeUntilDestroyed(this.destroyRef),
        map(_ => this.packData()),
        switchMap((data) => this.settingService.updateMetadataSettings(data)),
      ).subscribe();

    });

  }

  packData() {
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

    // Translate blacklist string -> Array<string>
    return {
      ...model,
      ageRatingMappings,
      blacklist: (model.blacklist || '').split(',').map((item: string) => item.trim())
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


}
