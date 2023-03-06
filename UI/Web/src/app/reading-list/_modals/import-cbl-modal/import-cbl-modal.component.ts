import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FileUploadValidators } from '@iplab/ngx-file-upload';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { CblBookResult } from 'src/app/_models/reading-list/cbl/cbl-book-result';
import { CblImportResult } from 'src/app/_models/reading-list/cbl/cbl-import-result.enum';
import { CblImportSummary } from 'src/app/_models/reading-list/cbl/cbl-import-summary';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { TimelineStep } from '../../_components/step-tracker/step-tracker.component';

interface FileStep {
  filename: string;
  validateSummary: CblImportSummary | undefined;
  dryRunSummary: CblImportSummary | undefined;
  finalizeSummary: CblImportSummary | undefined;
}

enum Step {
  Import = 0,
  Validate = 1,
  DryRun = 2,
  Finalize = 3
}

@Component({
  selector: 'app-import-cbl-modal',
  templateUrl: './import-cbl-modal.component.html',
  styleUrls: ['./import-cbl-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportCblModalComponent {

  @ViewChild('fileUpload') fileUpload!: ElementRef<HTMLInputElement>;

  fileUploadControl = new FormControl<undefined | Array<File>>(undefined, [
    FileUploadValidators.accept(['.cbl']),
  ]);
  
  uploadForm = new FormGroup({
    files: this.fileUploadControl
  });

  importSummaries: Array<CblImportSummary> = [];
  validateSummary: CblImportSummary | undefined;
  dryRunSummary: CblImportSummary | undefined;
  dryRunResults: Array<CblBookResult> = [];
  finalizeSummary: CblImportSummary | undefined;
  finalizeResults: Array<CblBookResult> = [];

  isLoading: boolean = false;

  steps: Array<TimelineStep> = [
    {title: 'Import CBLs', index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: 'Validate CBL', index: Step.Validate, active: false, icon: 'fa-solid fa-spell-check'},
    {title: 'Dry Run', index: Step.DryRun, active: false, icon: 'fa-solid fa-gears'},
    {title: 'Final Import', index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = this.steps[0].index;
  currentFileIndex: number = 0;

  files: Array<FileStep> = [];

  get Breakpoint() { return Breakpoint; }
  get Step() { return Step; }

  get FileCount() { 
    const files = this.uploadForm.get('files')?.value;
    if (!files) return 0;
    return files.length;
  }

  get NextButtonLabel() {
    switch(this.currentStepIndex) {
      case Step.DryRun:
        return 'Import';
      case Step.Finalize:
        return 'Restart'
      default:
        return 'Next';
    }
  }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef,
    private toastr: ToastrService) {}

  close() {
    this.ngModal.close();
  }

  nextStep() {
    if (this.currentStepIndex === Step.Import && !this.isFileSelected()) return;
    if (this.currentStepIndex === Step.Validate && this.validateSummary && this.validateSummary.results.length > 0) return;

    this.isLoading = true;
    switch (this.currentStepIndex) {
      case Step.Import:
        this.importFile();
        break;
      case Step.Validate:
        this.import(true);
        break;
      case Step.DryRun:
        this.import(false);
        break;
      case Step.Finalize:
        // Clear the models and allow user to do another import
        this.uploadForm.get('files')?.setValue(undefined);
        this.currentStepIndex = Step.Import;
        this.validateSummary = undefined;
        this.dryRunSummary = undefined;
        this.dryRunResults = [];
        this.finalizeSummary = undefined;
        this.finalizeResults = [];
        this.isLoading = false;
        this.cdRef.markForCheck();
        break;

    }
  }

  prevStep() {
    if (this.currentStepIndex === Step.Import) return;
    this.currentStepIndex--;
  }

  canMoveToNextStep() {
    switch (this.currentStepIndex) {
      case Step.Import:
        return this.isFileSelected();
      case Step.Validate:
        return this.validateSummary && this.validateSummary.results.length === 0;
      case Step.DryRun:
        return this.dryRunSummary?.success != CblImportResult.Fail; 
      case Step.Finalize:
        return true; 
      default:
        return false;
    }
  }

  canMoveToPrevStep() {
    switch (this.currentStepIndex) {
      case Step.Import:
        return false;
      default:
        return true;
    }
  }


  isFileSelected() {
    const files = this.uploadForm.get('files')?.value;
    if (files) return files.length > 0;
    return false;
  }

  importFile() {
    const files = this.uploadForm.get('files')?.value;
    if (!files) return;

    this.cdRef.markForCheck();

    const formData = new FormData();
    formData.append('cbl', files[this.currentFileIndex]);
    this.readingListService.validateCbl(formData).subscribe(res => {
      if (this.currentStepIndex === Step.Import) {
        this.validateSummary = res;
      }
      this.importSummaries.push(res);
      this.currentStepIndex++;
      this.currentFileIndex++;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  import(dryRun: boolean = false) {
    const files = this.uploadForm.get('files')?.value;
    if (!files) return;

    const formData = new FormData();
    formData.append('cbl', files[this.currentFileIndex]);
    formData.append('dryRun', dryRun + '');
    this.readingListService.importCbl(formData).subscribe(res => {
      // Our step when calling is always one behind
      if (dryRun) {
        this.dryRunSummary = res;
        this.dryRunResults = [...res.successfulInserts, ...res.results].sort((a, b) => a.order - b.order);
      } else {
        this.finalizeSummary = res;
        this.finalizeResults = [...res.successfulInserts, ...res.results].sort((a, b) => a.order - b.order);
        this.toastr.success('Reading List imported');
      }

      this.isLoading = false;
      this.currentStepIndex++;
      this.cdRef.markForCheck();
    });
  }
}
