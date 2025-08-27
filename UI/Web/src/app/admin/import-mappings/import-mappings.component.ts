import {Component, computed, inject, OnInit, signal, ViewChild} from '@angular/core';
import {translate, TranslocoDirective, TranslocoPipe} from "@jsverse/transloco";
import {StepTrackerComponent, TimelineStep} from "../../reading-list/_components/step-tracker/step-tracker.component";
import {WikiLink} from "../../_models/wiki";
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidatorFn,
  Validators
} from "@angular/forms";
import {FileUploadComponent, FileUploadValidators} from "@iplab/ngx-file-upload";
import {MetadataSettings} from "../_models/metadata-settings";
import {SettingsService} from "../settings.service";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {ToastrService} from "ngx-toastr";
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
import {map, tap} from "rxjs/operators";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {NgTemplateOutlet} from "@angular/common";
import {Router} from "@angular/router";
import {LicenseService} from "../../_services/license.service";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {toObservable, toSignal} from "@angular/core/rxjs-interop";

enum Step {
  Import = 0,
  Configure = 1,
  Conflicts = 2,
  Finalize = 3,
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
  ],
  templateUrl: './import-mappings.component.html',
  styleUrl: './import-mappings.component.scss'
})
export class ImportMappingsComponent implements OnInit {

  private readonly router = inject(Router);
  private readonly licenseService = inject(LicenseService);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  @ViewChild(ManageMetadataMappingsComponent) manageMetadataMappingsComponent!: ManageMetadataMappingsComponent;

  steps: TimelineStep[] = [
    {title: translate('import-mappings.import-step'), index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: translate('import-mappings.configure-step'), index: Step.Configure, active: false, icon: 'fa-solid fa-gears'},
    {title: translate('import-mappings.conflicts-step'), index: Step.Conflicts, active: false, icon: 'fa-solid fa-hammer'},
    {title: translate('import-mappings.finalize-step'), index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = signal(this.steps[0].index);

  fileUploadControl = new FormControl<undefined | Array<File>>(undefined, [
    FileUploadValidators.accept(['.json']), FileUploadValidators.filesLimit(1)
  ]);

  uploadForm = new FormGroup({
    files: this.fileUploadControl,
  });
  importSettingsForm = new FormGroup({
    importMode: new FormControl(ImportMode.Merge, [Validators.required]),
    resolution: new FormControl(ConflictResolution.Manual),
    whitelist: new FormControl(true),
    blacklist: new FormControl(true),
    ageRatings: new FormControl(true),
    fieldMappings: new FormControl(true),
    ageRatingConflictResolutions: new FormGroup({}),
  });
  /**
   * This is that contains the data in the finalize step
   */
  mappingsForm = new FormGroup({});

  isLoading = signal(false);
  settings = signal<MetadataSettings | undefined>(undefined)
  importedMappings = signal<MetadataMappingsExport | undefined>(undefined);
  importResult = signal<FieldMappingsImportResult | undefined>(undefined);

  isFileSelected = toSignal(this.uploadForm.get('files')!.valueChanges
    .pipe(map((files) => !!files && files.length == 1)), {initialValue: false});

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
        return this.importSettingsForm.valid;
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
    const data = this.manageMetadataMappingsComponent.packData();

    // Update settings with data from the final step
    newSettings.whitelist = data.whitelist;
    newSettings.blacklist = data.blacklist;
    newSettings.ageRatingMappings = data.ageRatingMappings;
    newSettings.fieldMappings = data.fieldMappings;

    this.settingsService.updateMetadataSettings(newSettings).subscribe({
      next: () => {
        const fragment = this.licenseService.hasValidLicenseSignal()
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

    const settings = this.importSettingsForm.value as ImportSettings;

    return firstValueFrom(this.settingsService.importFieldMappings(data, settings).pipe(
      catchError(err => {
        console.error(err);
        this.toastr.error(translate('import-mappings.invalid-file'));
        return of(null)
      }),
      switchMap((res) => {
        if (res == null) return of(null);

        this.importResult.set(res);

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
    const files = this.fileUploadControl.value;
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

  private setupSettingConflicts(res: FieldMappingsImportResult) {
    const ageRatingGroup = this.importSettingsForm.get('ageRatingConflictResolutions')! as FormGroup;

    for (let key of res.ageRatingConflicts) {
      if (!ageRatingGroup.get(key)) {
        ageRatingGroup.addControl(key, new FormControl(ConflictResolution.Manual, [this.notManualValidator()]))
      }
    }
  }

  private notManualValidator(): ValidatorFn {
    return (control: AbstractControl) => {
      const value = control.value;
      try {
        if (parseInt(value, 10) !== ConflictResolution.Manual) return null;
      } catch (e) {
      }

      return {'notManualValidator': {'value': value}}
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
      this.fileUploadControl.reset();
      (this.importSettingsForm.get('ageRatingConflictResolutions') as FormArray).clear();
    }

  }

  protected readonly Step = Step;
  protected readonly WikiLink = WikiLink;
  protected readonly ImportModes = ImportModes;
  protected readonly ConflictResolutions = ConflictResolutions;
  protected readonly ConflictResolution = ConflictResolution;
}
