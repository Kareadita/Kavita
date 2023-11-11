import {ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, inject, ViewChild} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {FileUploadModule, FileUploadValidators} from '@iplab/ngx-file-upload';
import {
  NgbAccordionModule, NgbAccordionToggle,
  NgbActiveModal
} from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { forkJoin } from 'rxjs';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { CblImportResult } from 'src/app/_models/reading-list/cbl/cbl-import-result.enum';
import { CblImportSummary } from 'src/app/_models/reading-list/cbl/cbl-import-summary';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import {StepTrackerComponent, TimelineStep} from '../../_components/step-tracker/step-tracker.component';
import {CommonModule} from "@angular/common";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {CblConflictReasonPipe} from "../../../_pipes/cbl-conflict-reason.pipe";
import {CblImportResultPipe} from "../../../_pipes/cbl-import-result.pipe";
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";

interface FileStep {
  fileName: string;
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
  standalone: true,
  imports: [CommonModule,
    FileUploadModule,
    NgbAccordionModule,
    SafeHtmlPipe,
    CblConflictReasonPipe, ReactiveFormsModule, StepTrackerComponent, CblImportResultPipe, NgbAccordionToggle, TranslocoDirective],
  templateUrl: './import-cbl-modal.component.html',
  styleUrls: ['./import-cbl-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportCblModalComponent {

  @ViewChild('fileUpload') fileUpload!: ElementRef<HTMLInputElement>;

  translocoService = inject(TranslocoService);

  fileUploadControl = new FormControl<undefined | Array<File>>(undefined, [
    FileUploadValidators.accept(['.cbl']),
  ]);

  uploadForm = new FormGroup({
    files: this.fileUploadControl
  });

  isLoading: boolean = false;

  steps: Array<TimelineStep> = [
    {title: this.translocoService.translate('import-cbl-modal.import-step'), index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: this.translocoService.translate('import-cbl-modal.validate-cbl-step'), index: Step.Validate, active: false, icon: 'fa-solid fa-spell-check'},
    {title: this.translocoService.translate('import-cbl-modal.dry-run-step'), index: Step.DryRun, active: false, icon: 'fa-solid fa-gears'},
    {title: this.translocoService.translate('import-cbl-modal.final-import-step'), index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = this.steps[0].index;

  filesToProcess: Array<FileStep> = [];
  failedFiles: Array<FileStep> = [];


  get Breakpoint() { return Breakpoint; }
  get Step() { return Step; }
  get CblImportResult() { return CblImportResult; }

  get NextButtonLabel() {
    switch(this.currentStepIndex) {
      case Step.DryRun:
        return 'import';
      case Step.Finalize:
        return 'restart'
      default:
        return 'next';
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

    this.isLoading = true;
    switch (this.currentStepIndex) {
      case Step.Import:
        const files = this.uploadForm.get('files')?.value;
        if (!files) {
          this.toastr.error(this.translocoService.translate('toasts.select-files-warning'));
          return;
        }
        // Load each file into filesToProcess and group their data
        let pages = [];
        for (let i = 0; i < files.length; i++) {
          const formData = new FormData();
            formData.append('cbl', files[i]);
            formData.append('dryRun', true + '');
            pages.push(this.readingListService.validateCbl(formData));
        }
        forkJoin(pages).subscribe(results => {
          this.filesToProcess = [];
          results.forEach(cblImport => {
            this.filesToProcess.push({
              fileName: cblImport.fileName,
              validateSummary: cblImport,
              dryRunSummary: undefined,
              finalizeSummary: undefined
            });
          });

          this.filesToProcess = this.filesToProcess.sort((a, b) => b.validateSummary!.success - a.validateSummary!.success);
          this.cdRef.markForCheck();

          this.currentStepIndex++;
          this.isLoading = false;
          this.cdRef.markForCheck();
        });
        break;
      case Step.Validate:
        this.failedFiles = this.filesToProcess.filter(item => item.validateSummary?.success === CblImportResult.Fail);
        this.filesToProcess = this.filesToProcess.filter(item => item.validateSummary?.success != CblImportResult.Fail);
        this.dryRun();
        break;
      case Step.DryRun:
        this.failedFiles.push(...this.filesToProcess.filter(item => item.dryRunSummary?.success === CblImportResult.Fail));
        this.filesToProcess = this.filesToProcess.filter(item => item.dryRunSummary?.success != CblImportResult.Fail);
        this.import();
        break;
      case Step.Finalize:
        // Clear the models and allow user to do another import
        this.uploadForm.get('files')?.setValue(undefined);
        this.currentStepIndex = Step.Import;
        this.isLoading = false;
        this.filesToProcess = [];
        this.failedFiles = [];
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
        return this.filesToProcess.filter(item => item.validateSummary?.success != CblImportResult.Fail).length > 0;
      case Step.DryRun:
        return this.filesToProcess.filter(item => item.dryRunSummary?.success != CblImportResult.Fail).length > 0;
      case Step.Finalize:
        return true;
      default:
        return false;
    }
  }

  canMoveToPrevStep() {
    switch (this.currentStepIndex) {
      case Step.Import:
      case Step.Finalize:
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


  dryRun() {
    const filenamesAllowedToProcess = this.filesToProcess.map(p => p.fileName);
    const files = (this.uploadForm.get('files')?.value || []).filter(f => filenamesAllowedToProcess.includes(f.name));

    let pages = [];
    for (let i = 0; i < files.length; i++) {
      const formData = new FormData();
        formData.append('cbl', files[i]);
        formData.append('dryRun', 'true');
        pages.push(this.readingListService.importCbl(formData));
    }
    forkJoin(pages).subscribe(results => {
        results.forEach(cblImport => {
        const index = this.filesToProcess.findIndex(p => p.fileName === cblImport.fileName);
        this.filesToProcess[index].dryRunSummary = cblImport;
      });
      this.filesToProcess = this.filesToProcess.sort((a, b) => b.dryRunSummary!.success - a.dryRunSummary!.success);

      this.isLoading = false;
      this.currentStepIndex++;
      this.cdRef.markForCheck();
    });
  }

  import() {
    const filenamesAllowedToProcess = this.filesToProcess.map(p => p.fileName);
    const files = (this.uploadForm.get('files')?.value || []).filter(f => filenamesAllowedToProcess.includes(f.name));

    let pages = [];
    for (let i = 0; i < files.length; i++) {
      const formData = new FormData();
      formData.append('cbl', files[i]);
      formData.append('dryRun', 'false');
      pages.push(this.readingListService.importCbl(formData));
    }
    forkJoin(pages).subscribe(results => {
      results.forEach(cblImport => {
        const index = this.filesToProcess.findIndex(p => p.fileName === cblImport.fileName);
        this.filesToProcess[index].finalizeSummary = cblImport;
      });

      this.isLoading = false;
      this.currentStepIndex++;
      this.toastr.success(this.translocoService.translate('toasts.reading-list-imported'));
      this.cdRef.markForCheck();
    });
  }
}
