import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FileUploadValidators } from '@iplab/ngx-file-upload';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { CblImportSummary } from 'src/app/_models/reading-list/cbl/cbl-import-summary';
import { ReadingListService } from 'src/app/_services/reading-list.service';

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

  steps = [
    {title: 'Import CBL', index: Step.Import},
    {title: 'Validate File', index: Step.Validate},
    {title: 'Dry Run', index: Step.DryRun},
    {title: 'Final Import', index: Step.Finalize},
  ];
  currentStep = this.steps[0];

  get Breakpoint() { return Breakpoint; }
  get Step() { return Step; }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef) {}

  close() {
    this.ngModal.close();
  }

  nextStep() {

    if (this.currentStep.index >= Step.Finalize) return;
    if (this.currentStep.index === Step.Import && !this.isFileSelected()) return;
    if (this.currentStep.index === Step.Validate && this.validateSummary && this.validateSummary.results.length > 0) return;

    switch (this.currentStep.index) {
      case Step.Import:
        this.importFile();
        break;
      case Step.Validate:
        break;
      case Step.DryRun:
        break;
      case Step.Finalize:
        // Clear the models and allow user to do another import
        break;

    }
  }

  canMoveToNextStep() {
    switch (this.currentStep.index) {
      case Step.Import:
        return this.isFileSelected();
      case Step.Validate:
        return this.validateSummary && this.validateSummary.results.length > 0;
      case Step.DryRun:
        return true; 
      case Step.Finalize:
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
    this.readingListService.importCbl(formData).subscribe(res => {
      console.log('Result: ', res);
      this.validateSummary = res;
      this.importSummaries.push(res);
      this.currentStep.index++;
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
