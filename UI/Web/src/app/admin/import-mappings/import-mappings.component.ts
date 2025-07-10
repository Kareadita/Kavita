import {Component, inject, OnInit, signal} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {StepTrackerComponent, TimelineStep} from "../../reading-list/_components/step-tracker/step-tracker.component";
import {WikiLink} from "../../_models/wiki";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {FileUploadComponent, FileUploadValidators} from "@iplab/ngx-file-upload";
import {AgeRating} from "../../_models/metadata/age-rating";
import {MetadataFieldMapping, MetadataSettings} from "../_models/metadata-settings";
import {SettingsService} from "../settings.service";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {MetadataMappingsExport} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {ToastrService} from "ngx-toastr";
import {LoadingComponent} from "../../shared/loading/loading.component";

enum Step {
  Import = 0,
  Configure = 1,
  Overview = 2,
  Finalize= 3,
}

type AgeRatingConflict = {
  tag: string;
  currentRating: AgeRating;
  newRating: AgeRating;
}

type FieldMappingConflict = {
  tag: string;
  currentMapping: MetadataFieldMapping;
  newMappings: MetadataFieldMapping;
}

enum ImportMode {
  Replace = 0,
  Merge = 1,
}

type ImportSettings = {
  importMode: ImportMode;
  whitelist: boolean;
  blacklist: boolean;
  ageRatings: boolean;
  fieldMappings: boolean;
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
    {title: translate('import-mappings.overview-step'), index: Step.Overview, active: false, icon: 'fa-solid fa-sliders'},
    {title: translate('import-mappings.finalize-step'), index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = signal(this.steps[0].index);

  fileUploadControl = new FormControl<undefined | Array<File>>(undefined, [
    FileUploadValidators.accept(['.json']), FileUploadValidators.filesLimit(1)
  ]);

  uploadForm = new FormGroup({
    files: this.fileUploadControl
  });

  isLoading = signal(false);
  settings = signal<MetadataSettings | undefined>(undefined)
  importedSettings = signal<MetadataMappingsExport | undefined>(undefined);

  ageRatingConflicts = signal<AgeRatingConflict[]>([]);
  fieldMappingConflicts = signal<FieldMappingConflict[]>([]);

  ngOnInit(): void {
    this.settingsService.getMetadataSettings().subscribe((settings) => {
      this.settings.set(settings);
    });
  }


  get NextButtonLabel() {
    switch(this.currentStepIndex()) {
      case Step.Overview:
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
        const files = this.fileUploadControl.value;
        if (!files || files.length === 0) {
          this.toastr.error(translate('import-mappings.select-files-warning'));
          break;
        }

        const file = files[0];
        let newImport: MetadataMappingsExport;
        try {
          newImport = JSON.parse(await file.text()) as MetadataMappingsExport;
        } catch (error) {
          this.toastr.error(translate('import-mappings.invalid-file'));
          break;
        }
        if (!newImport.fieldMappings && !newImport.ageRatingMappings && !newImport.blacklist && !newImport.whitelist) {
          this.toastr.error(translate('import-mappings.file-no-valid-content'));
          break;
        }

        this.importedSettings.set(newImport);
        this.currentStepIndex.update(x=>x+1);
        break;
    }

    this.isLoading.set(false);
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
      case Step.Overview:
        return this.ageRatingConflicts().length === 0 && this.fieldMappingConflicts().length == 0;
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
      case Step.Finalize:
        return false;
      default:
        return true;
    }
  }

  protected readonly Step = Step;
  protected readonly WikiLink = WikiLink;
}
