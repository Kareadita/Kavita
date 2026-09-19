import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, viewChild} from '@angular/core';
import {translate, TranslocoDirective, TranslocoPipe} from "@jsverse/transloco";
import {StepTrackerComponent, TimelineStep} from "../../reading-list/_components/step-tracker/step-tracker.component";
import {WikiLink} from "../../_models/wiki";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {FileUploadComponent} from "@iplab/ngx-file-upload";
import {MetadataSettings} from "../_models/metadata-settings";
import {SettingsService} from "../settings.service";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport,
  MetadataMappingsFormModel,
  metadataMappingsSchema,
  packFieldMappings,
  toMetadataMappingsFormModel
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {ToastrService} from '@openng/ngx-toastr';
import {LoadingComponent} from "../../shared/loading/loading.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {ImportModePipe} from "../../_pipes/import-mode.pipe";
import {ConflictResolutionPipe} from "../../_pipes/conflict-resolution.pipe";
import {
  ConflictResolution,
  ConflictResolutions,
  FieldMappingsImportResult,
  ImportMode,
  ImportModes,
  ImportSettings
} from "../../_models/import-field-mappings";
import {catchError, firstValueFrom, of, switchMap} from "rxjs";
import {tap} from "rxjs/operators";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {NgTemplateOutlet} from "@angular/common";
import {Router} from "@angular/router";
import {LicenseService} from "../../_services/license.service";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {applyEach, apply, form, FormField, FormRoot, required, validate} from "@angular/forms/signals";
import {packAgeRatingMappings} from "../../shared/_components/age-rating-mapper/age-rating-mapper.component";
import {SettingSelectComponent} from "../../settings/_components/setting-enum-select/setting-select.component";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";

enum Step {
  Import = 0,
  Configure = 1,
  Conflicts = 2,
  Finalize = 3,
}

interface FileFormModel {
  files: File[];
}

function defaultImportSettings(): ImportSettings {
  return {
    importMode: ImportMode.Merge,
    resolution: ConflictResolution.Manual,
    ageRatingConflictResolutions: {},
    ageRatings: true,
    blacklist: true,
    fieldMappings: true,
    whitelist: true
  };
}


@Component({
  selector: 'app-import-mappings',
  imports: [
    TranslocoDirective,
    StepTrackerComponent,
    FileUploadComponent,
    FormsModule,
    ReactiveFormsModule,
    LoadingComponent,
    SettingSwitchComponent,
    SettingItemComponent,
    ImportModePipe,
    ConflictResolutionPipe,
    AgeRatingPipe,
    NgTemplateOutlet,
    TranslocoPipe,
    ManageMetadataMappingsComponent,
    FormField,
    FormRoot,
    SettingSelectComponent,
    ValidationErrorsComponent,
  ],
  templateUrl: './import-mappings.component.html',
  styleUrl: './import-mappings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportMappingsComponent implements OnInit {

  private readonly router = inject(Router);
  private readonly licenseService = inject(LicenseService);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  steps: TimelineStep[] = [
    {title: translate('import-mappings.import-step'), index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: translate('import-mappings.configure-step'), index: Step.Configure, active: false, icon: 'fa-solid fa-gears'},
    {title: translate('import-mappings.conflicts-step'), index: Step.Conflicts, active: false, icon: 'fa-solid fa-hammer'},
    {title: translate('import-mappings.finalize-step'), index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = signal(this.steps[0].index);

  private readonly fileFormModel = signal<FileFormModel>({
    files: []
  });

  private readonly formModel = signal<ImportSettings>(defaultImportSettings());

  fileFormGroup = form(this.fileFormModel, p => {

    // Only json files may be uploaded
    validate(p.files, ({value}) => {
      const files = value();
      if (!files || files.length === 0) return null;

      if (files.every(f => f.name.toLowerCase().endsWith('.json'))) {
        return null;
      }

      return { kind: 'fileType', message: translate('import-mappings.select-files-warning') };
    });

    // Only a single file may be uploaded
    validate(p.files, ({value}) => {
      const files = value();
      if (!files || files.length <= 1) return null;

      return { kind: 'fileLimit', message: translate('import-mappings.select-files-warning') };
    });
  });
  formGroup = form(this.formModel, p => {
    required(p.importMode);

    // Every age rating collision must be explicitly resolved before the import can continue
    applyEach(p.ageRatingConflictResolutions, resolution => {
      validate(resolution, ({value}) => {
        if (value() !== ConflictResolution.Manual) return null;

        return { kind: 'notManual', message: translate('import-mappings.to-pick') };
      });
    });
  });

  /**
   * Holds the mapping data shown in the finalize step, seeded from the import result
   */
  private readonly mappingsModel = signal<MetadataMappingsFormModel>({
    enableGenres: false,
    enableTags: false,
    filterAboveWeight: null,
    blacklist: [],
    whitelist: [],
    ageRatingMappings: [],
    externalAgeRatingMappings: [],
    fieldMappings: [],
  });
  protected readonly mappingsGroup = form(this.mappingsModel, p => apply(p, metadataMappingsSchema));

  isLoading = signal(false);
  settings = signal<MetadataSettings | undefined>(undefined)
  importedMappings = signal<MetadataMappingsExport | undefined>(undefined);
  importResult = signal<FieldMappingsImportResult | undefined>(undefined);

  isFileSelected = computed(() => {
    const files = this.fileFormGroup.files().value();
    return !!files && files.length == 1;
  });

  nextButtonLabel = computed(() => {
    switch(this.currentStepIndex()) {
      case Step.Configure:
      case Step.Conflicts:
        return 'import';
      case Step.Finalize:
        return 'save';
      default:
        return 'next';
    }
  });

  canMoveToNextStep = computed(() => {
    switch (this.currentStepIndex()) {
      case Step.Import:
        return this.isFileSelected();
      case Step.Finalize:
      case Step.Configure:
        return true;
      case Step.Conflicts:
        return this.formGroup().valid();
      default:
        return false;
    }
  });

  canMoveToPrevStep = computed(() => {
    switch (this.currentStepIndex()) {
      case Step.Import:
        return false;
      default:
        return true;
    }
  });

  ngOnInit(): void {
    this.settingsService.getMetadataSettings().subscribe((settings) => {
      this.settings.set(settings);
    });
  }

  async nextStep() {
    if (this.currentStepIndex() === Step.Import && !this.isFileSelected()) return;

    this.isLoading.set(true);
    try {
      switch(this.currentStepIndex()) {
        case Step.Import:
          await this.validateImport();
          break;
        case Step.Conflicts:
        case Step.Configure:
          await this.tryImport();
          break;
        case Step.Finalize:
          this.save();
      }
    } catch (error) {
      /** Swallow **/
    }

    this.isLoading.set(false);
  }

  save() {
    const res = this.importResult();
    if (!res) return;

    const newSettings = res.resultingMetadataSettings;
    const mappings = this.mappingsModel();

    // Update settings with data from the final step
    newSettings.whitelist = mappings.whitelist;
    newSettings.blacklist = mappings.blacklist;
    newSettings.ageRatingMappings = packAgeRatingMappings(mappings.ageRatingMappings);
    newSettings.fieldMappings = packFieldMappings(mappings.fieldMappings);

    this.settingsService.updateMetadataSettings(newSettings).subscribe({
      next: () => {
        const fragment = this.licenseService.hasActiveLicense()
          ? SettingsTabId.Metadata : SettingsTabId.ManageMetadata;

        this.router.navigate(['settings'], { fragment: fragment });
      }
    });
  }

  async tryImport() {
    const data = this.importedMappings();
    if (!data) {
      this.toastr.error(translate('import-mappings.file-no-valid-content'));
      return Promise.resolve();
    }

    const settings = this.formModel();

    return firstValueFrom(this.settingsService.importFieldMappings(data, settings).pipe(
      catchError(err => {
        console.error(err);
        this.toastr.error(translate('import-mappings.invalid-file'));
        return of(null)
      }),
      switchMap((res) => {
        if (res == null) return of(null);

        this.importResult.set(res);
        this.mappingsModel.set(toMetadataMappingsFormModel(res.resultingMetadataSettings));

        return this.settingsService.getMetadataSettings().pipe(
          tap(dto => this.settings.set(dto)),
          tap(() => {
            if (res.success) {
              this.currentStepIndex.set(Step.Finalize);
              return;
            }

            this.setupSettingConflicts(res);
            this.currentStepIndex.set(Step.Conflicts);
          }),
        )}),
      ));
  }

  async validateImport() {
    const files = this.fileFormModel().files;
    if (!files || files.length === 0) {
      this.toastr.error(translate('import-mappings.select-files-warning'));
      return;
    }

    const file = files[0];
    let newImport: MetadataMappingsExport;
    try {
      newImport = JSON.parse(await file.text()) as MetadataMappingsExport;
    } catch (error) {
      this.toastr.error(translate('import-mappings.invalid-file'));
      return;
    }
    if (!newImport.fieldMappings && !newImport.ageRatingMappings && !newImport.blacklist && !newImport.whitelist) {
      this.toastr.error(translate('import-mappings.file-no-valid-content'));
      return;
    }

    this.importedMappings.set(newImport);
    this.currentStepIndex.update(x => x + 1);
  }

  /**
   * Seeds a resolution field for every collision the server reported. The record is rebuilt from the
   * response so collisions from a previous attempt don't linger and hold the form invalid, but a
   * choice the user already made for a key that is still in conflict is carried over.
   */
  private setupSettingConflicts(res: FieldMappingsImportResult) {
    const existing = this.formModel().ageRatingConflictResolutions;
    const resolutions: Record<string, ConflictResolution> = {};

    for (const key of res.ageRatingConflicts) {
      resolutions[key] = existing[key] ?? ConflictResolution.Manual;
    }

    this.formModel.update(model => ({...model, ageRatingConflictResolutions: resolutions}));

    // The user is being asked to resolve these, so surface the "unresolved" message straight away
    // rather than waiting for a blur that may never come (Next is disabled until they all resolve).
    for (const key of res.ageRatingConflicts) {
      this.formGroup.ageRatingConflictResolutions[key]().markAsTouched();
    }
  }

  prevStep() {
    if (this.currentStepIndex() === Step.Import) return;

    if (this.currentStepIndex() === Step.Finalize) {
      if (this.importResult()!.ageRatingConflicts.length === 0) {
        this.currentStepIndex.set(Step.Configure);
      } else {
        this.currentStepIndex.set(Step.Conflicts);
      }
      return;
    }

    this.currentStepIndex.update(x => x - 1);

    // Reset when returning to the first step
    if (this.currentStepIndex() === Step.Import) {
      this.fileFormModel.set({files: []});
      this.fileFormGroup().reset();

      this.formModel.update(model => ({...model, ageRatingConflictResolutions: {}}));
      this.formGroup().reset();
    }

  }

  protected readonly Step = Step;
  protected readonly WikiLink = WikiLink;
  protected readonly ImportModes = ImportModes;
  protected readonly ConflictResolutions = ConflictResolutions;
  protected readonly ConflictResolution = ConflictResolution;
}
