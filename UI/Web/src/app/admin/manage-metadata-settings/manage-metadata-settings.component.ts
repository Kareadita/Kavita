import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingsService} from "../settings.service";
import {debounceTime, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {MetadataSettings} from "../_models/metadata-settings";
import {PersonRole} from "../../_models/metadata/person";
import {PersonRolePipe} from "../../_pipes/person-role.pipe";
import {allMetadataSettingField, MetadataSettingField} from "../_models/metadata-setting-field";
import {MetadataSettingFiledPipe} from "../../_pipes/metadata-setting-filed.pipe";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {AgeRating} from "../../_models/metadata/age-rating";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";


@Component({
  selector: 'app-manage-metadata-settings',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingSwitchComponent,
    PersonRolePipe,
    MetadataSettingFiledPipe,
    ManageMetadataMappingsComponent,
    RouterLink,

  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  @ViewChild(ManageMetadataMappingsComponent) manageMetadataMappingsComponent!: ManageMetadataMappingsComponent;

  private readonly settingService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  settingsForm: FormGroup = new FormGroup({});
  settings: MetadataSettings | undefined = undefined;
  personRoles: PersonRole[] = [PersonRole.Writer, PersonRole.CoverArtist, PersonRole.Character];
  isLoaded = false;
  allMetadataSettingFields = allMetadataSettingField;

  ngOnInit(): void {
    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settings = settings;
      this.cdRef.markForCheck();

      this.settingsForm.addControl('enabled', new FormControl(settings.enabled, []));
      this.settingsForm.addControl('enableExtendedMetadataProcessing', new FormControl(settings.enableExtendedMetadataProcessing, []));
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


      this.settingsForm.addControl('enableChapterTitle', new FormControl(settings.enableChapterTitle, []));
      this.settingsForm.addControl('enableChapterSummary', new FormControl(settings.enableChapterSummary, []));
      this.settingsForm.addControl('enableChapterReleaseDate', new FormControl(settings.enableChapterReleaseDate, []));
      this.settingsForm.addControl('enableChapterPublisher', new FormControl(settings.enableChapterPublisher, []));
      this.settingsForm.addControl('enableChapterCoverImage', new FormControl(settings.enableChapterCoverImage, []));

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

  packData(withFieldMappings: boolean = true) {
    const model = this.settingsForm.value;

    const exp: MetadataMappingsExport = this.manageMetadataMappingsComponent.packData()

    return {
      ...model,
      ageRatingMappings: exp.ageRatingMappings,
      fieldMappings: withFieldMappings ? exp.fieldMappings : [],
      blacklist: exp.blacklist,
      whitelist: exp.whitelist,
      personRoles: Object.entries(this.settingsForm.get('personRoles')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.personRoles[parseInt(key.split('_')[1], 10)]),
      overrides: Object.entries(this.settingsForm.get('overrides')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.allMetadataSettingFields[parseInt(key.split('_')[1], 10)])
    }
  }


  protected readonly SettingsTabId = SettingsTabId;
}
