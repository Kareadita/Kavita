import {Component, inject, OnInit, signal} from '@angular/core';
import {translate, TranslocoDirective, TranslocoPipe} from "@jsverse/transloco";
import {StepTrackerComponent, TimelineStep} from "../../reading-list/_components/step-tracker/step-tracker.component";
import {WikiLink} from "../../_models/wiki";
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidatorFn,
  Validators
} from "@angular/forms";
import {FileUploadComponent, FileUploadValidators} from "@iplab/ngx-file-upload";
import {MetadataFieldMapping, MetadataSettings} from "../_models/metadata-settings";
import {SettingsService} from "../settings.service";
import {MetadataMappingsExport} from "../manage-metadata-mappings/manage-metadata-mappings.component";
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
import {firstValueFrom, of, switchAll, switchMap} from "rxjs";
import {tap} from "rxjs/operators";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {NgTemplateOutlet} from "@angular/common";
import {MetadataFieldTypePipe} from "../../_pipes/metadata-field-type.pipe";

enum Step {
  Import = 0,
  Configure = 1,
  Conflicts = 2,
  Success= 3,
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
    MetadataFieldTypePipe,
  ],
  templateUrl: './import-mappings.component.html',
  styleUrl: './import-mappings.component.scss'
})
export class ImportMappingsComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  steps: TimelineStep[] = [
    {title: translate('import-mappings.import-step'), index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: translate('import-mappings.configure-step'), index: Step.Configure, active: false, icon: 'fa-solid fa-gears'},
    {title: translate('import-mappings.conflicts-step'), index: Step.Conflicts, active: false, icon: 'fa-solid fa-hammer'},
    {title: translate('import-mappings.success-step'), index: Step.Success, active: false, icon: 'fa-solid fa-check-double'},
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
    fieldMappingsConflictResolutions: new FormGroup({}),
  });

  isLoading = signal(false);
  settings = signal<MetadataSettings | undefined>(undefined)
  importedMappings = signal<MetadataMappingsExport | undefined>(undefined);
  importResult = signal<FieldMappingsImportResult | undefined>(undefined);

  ngOnInit(): void {
    this.settingsService.getMetadataSettings().subscribe((settings) => {
      this.settings.set(settings);
    });
  }


  get NextButtonLabel() {
    switch(this.currentStepIndex()) {
      case Step.Configure:
      case Step.Conflicts:
        return 'import'
      default:
        return 'next'
    }
  }

  async nextStep() {
    if (this.currentStepIndex() === Step.Import && !this.isFileSelected()) return;

    this.isLoading.set(true);
    switch(this.currentStepIndex()) {
      case Step.Import:
        await this.validateImport();
        break;
      case Step.Conflicts:
      case Step.Configure:
        await this.tryImport();
        break;
    }

    this.isLoading.set(false);
  }

  async tryImport() {
    const data = this.importedMappings();
    if (!data) {
      this.toastr.error(translate('import-mappings.file-no-valid-content'));
      return Promise.resolve();
    }

    const settings = this.importSettingsForm.value as ImportSettings;
    settings.resolution = parseInt(settings.resolution+'');
    settings.importMode = parseInt(settings.importMode+'');
    settings.ageRatingConflictResolutions = this.mapRecord(settings.ageRatingConflictResolutions, k => k, v => parseInt(v+''))
    settings.fieldMappingsConflictResolutions = this.mapRecord(settings.fieldMappingsConflictResolutions, k => parseInt(k+''), v => parseInt(v+''))

    return firstValueFrom(this.settingsService.importFieldMappings(data, settings).pipe(
      tap((res) => this.importResult.set(res)),
      switchMap((res) => {
        if (res.success) {
          this.currentStepIndex.set(Step.Success)
          return of(null);
        }

        // If we find conflicts, update settings to we are certain we have the most up to date copy
        return this.settingsService.getMetadataSettings().pipe(
          tap((dto) => {
            this.settings.set(dto);
            this.setupSettingConflicts(res, dto);
            this.currentStepIndex.set(Step.Conflicts)
          })
        );
      }),
    )).then(() => {});
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
    this.currentStepIndex.update(x=>x+1);
  }

  private setupSettingConflicts(res: FieldMappingsImportResult, settings: MetadataSettings) {
    const ageRatingGroup = this.importSettingsForm.get('ageRatingConflictResolutions')! as FormGroup;
    const fieldMappingsGroup = this.importSettingsForm.get('fieldMappingsConflictResolutions')! as FormGroup;

    for (let key of res.ageRatingConflicts) {
      if (!ageRatingGroup.get(key)) {
        ageRatingGroup.addControl(key, new FormControl(ConflictResolution.Manual, [this.notManualValidator()]))
      }
    }
    for (let key of res.fieldMappingConflicts) {
      if (!fieldMappingsGroup.get(`${key.oldId}`)) {
        fieldMappingsGroup.addControl(`${key.oldId}`, new FormControl(ConflictResolution.Manual, [this.notManualValidator()]))
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
    this.currentStepIndex.update(x => x-1);
  }

  canMoveToNextStep() {
    switch (this.currentStepIndex()) {
      case Step.Import:
        return this.isFileSelected();
      case Step.Configure:
        return true;
      case Step.Conflicts:
        const res = this.importResult();
        return this.importSettingsForm.valid;
      default:
          return false;
    }
  }

  isFileSelected() {
    const files = this.uploadForm.get('files')?.value;
    return files && files.length === 1;
  }

  canMoveToPrevStep() {
    switch (this.currentStepIndex()) {
      case Step.Import:
      case Step.Success:
        return false;
      default:
        return true;
    }
  }

  /**
   * Get an entry from a map, that's actually an object
   * @param m
   * @param key
   */
  getMapEntry<K, V>(m: Map<K, V>, key: K): V {
    return (m as any)[key] as V;
  }

  getFieldMapping(mappings: MetadataFieldMapping[], id: number) {
    return mappings.find(mapping => mapping.id === id)!;
  }

  private mapRecord<T extends string | number | symbol, V, T2 extends string | number | symbol, V2>(
    input: Record<T, V>,
    keyTransform: (key: T) => T2,
    valueTransform: (value: V) => V2
  ): Record<T2, V2> {
    return Object.fromEntries(
      Object.entries(input)
        .map(([key, value]) =>
          [keyTransform(key as T), valueTransform(value as V)])
    ) as Record<T2, V2>;
  }

  protected readonly Step = Step;
  protected readonly WikiLink = WikiLink;
  protected readonly ImportModes = ImportModes;
  protected readonly ConflictResolutions = ConflictResolutions;
  protected readonly ConflictResolution = ConflictResolution;
}
