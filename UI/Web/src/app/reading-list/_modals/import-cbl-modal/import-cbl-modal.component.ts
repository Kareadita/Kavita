import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FileUploadValidators } from '@iplab/ngx-file-upload';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { CblImportSummary } from 'src/app/_models/reading-list/cbl/cbl-import-summary';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { TimelineStep } from '../../_components/step-tracker/step-tracker.component';

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
    FileUploadValidators.filesLimit(1), 
    FileUploadValidators.accept(['.cbl']),
  ]);
  
  uploadForm = new FormGroup({
    files: this.fileUploadControl
  });

  importSummaries: Array<CblImportSummary> = [];
  validateSummary: CblImportSummary | undefined;
  dryRunSummary: CblImportSummary | undefined;

  steps: Array<TimelineStep> = [
    {title: 'Import CBL', index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: 'Validate File', index: Step.Validate, active: false, icon: 'fa-solid fa-spell-check'},
    {title: 'Dry Run', index: Step.DryRun, active: false, icon: 'fa-regular fa-floppy-disk'},
    {title: 'Final Import', index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = this.steps[0].index;

  get Breakpoint() { return Breakpoint; }
  get Step() { return Step; }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef) {}

  close() {
    this.ngModal.close();
  }

  nextStep() {

    if (this.currentStepIndex >= Step.Finalize) return;
    if (this.currentStepIndex === Step.Import && !this.isFileSelected()) return;
    if (this.currentStepIndex === Step.Validate && this.validateSummary && this.validateSummary.results.length > 0) return;

    switch (this.currentStepIndex) {
      case Step.Import:
        this.importFile();
        break;
      case Step.Validate:
        this.import();
        break;
      case Step.DryRun:
        this.import();
        break;
      case Step.Finalize:
        // Clear the models and allow user to do another import
        this.uploadForm.get('files')?.setValue(null);
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
        return true; 
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

    const formData = new FormData();
    formData.append('cbl', files[0]);
    this.readingListService.validateCbl(formData).subscribe(res => {
      console.log('Result: ', res);
      if (this.currentStepIndex === Step.Import) {
        this.validateSummary = res;
      }
      if (this.currentStepIndex === Step.DryRun) {
        this.dryRunSummary = res;
      }
      this.importSummaries.push(res);
      this.currentStepIndex++;
      this.cdRef.markForCheck();
    });
  }

  import() {
    const files = this.uploadForm.get('files')?.value;
    if (!files) return;

    const formData = new FormData();
    formData.append('cbl', files[0]);
    formData.append('dryRun', (this.currentStepIndex !== Step.Finalize) + '');
    this.readingListService.importCbl(formData).subscribe(res => {
      console.log('Step: ', this.currentStepIndex)
      console.log('Result: ', res);

      // Our step when calling is always one behind
      if (this.currentStepIndex === Step.Validate) {
        this.dryRunSummary = res;
      }
      this.importSummaries.push(res);
      this.currentStepIndex++;
      this.cdRef.markForCheck();
    });
  }

  // onFileSelected(event: any) {
  //   console.log('event: ', event);
  //   if (!(event.target as HTMLInputElement).files === null || (event.target as HTMLInputElement).files?.length === 0) return;

  //   const file = (event.target as HTMLInputElement).files![0];

  //   if (file) {

  //       //this.fileName = file.name;

  //       const formData = new FormData();

  //       formData.append("cbl", file);

  //       this.readingListService.importCbl(formData).subscribe(res => {
  //         this.importSummaries.push(res);
  //         this.cdRef.markForCheck();
  //       });
  //       this.fileUpload.value = '';
  //   }
  // }
}
