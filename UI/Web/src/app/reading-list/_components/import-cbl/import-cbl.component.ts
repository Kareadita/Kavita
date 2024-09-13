import {ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, inject, ViewChild} from '@angular/core';
import {CblConflictReasonPipe} from "../../../_pipes/cbl-conflict-reason.pipe";
import {CblImportResultPipe} from "../../../_pipes/cbl-import-result.pipe";
import {FileUploadComponent, FileUploadValidators} from "@iplab/ngx-file-upload";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgTemplateOutlet} from "@angular/common";
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem,
} from "@ng-bootstrap/ng-bootstrap";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {StepTrackerComponent, TimelineStep} from "../step-tracker/step-tracker.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingListService} from "../../../_services/reading-list.service";
import {UtilityService} from "../../../shared/_services/utility.service";
import {ToastrService} from "ngx-toastr";
import {forkJoin} from "rxjs";
import {CblImportSummary} from "../../../_models/reading-list/cbl/cbl-import-summary";
import { WikiLink } from 'src/app/_models/wiki';
import { CblImportResult } from 'src/app/_models/reading-list/cbl/cbl-import-result.enum';


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
  selector: 'app-import-cbl',
  standalone: true,
  imports: [
    CblConflictReasonPipe,
    CblImportResultPipe,
    FileUploadComponent,
    FormsModule,
    NgbAccordionBody,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionDirective,
    NgbAccordionHeader,
    NgbAccordionItem,
    ReactiveFormsModule,
    SafeHtmlPipe,
    StepTrackerComponent,
    TranslocoDirective,
    NgTemplateOutlet
  ],
  templateUrl: './import-cbl.component.html',
  styleUrl: './import-cbl.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportCblComponent {
  private readonly readingListService = inject(ReadingListService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly utilityService = inject(UtilityService);


  protected readonly CblImportResult = CblImportResult;
  protected readonly Step = Step;
  protected readonly WikiLink = WikiLink;

  @ViewChild('fileUpload') fileUpload!: ElementRef<HTMLInputElement>;


  fileUploadControl = new FormControl<undefined | Array<File>>(undefined, [
    FileUploadValidators.accept(['.cbl']),
  ]);

  uploadForm = new FormGroup({
    files: this.fileUploadControl
  });
  cblSettingsForm = new FormGroup({
    comicVineMatching: new FormControl(true, [])
  });

  isLoading: boolean = false;

  steps: Array<TimelineStep> = [
    {title: translate('import-cbl-modal.import-step'), index: Step.Import, active: true, icon: 'fa-solid fa-file-arrow-up'},
    {title: translate('import-cbl-modal.validate-cbl-step'), index: Step.Validate, active: false, icon: 'fa-solid fa-spell-check'},
    {title: translate('import-cbl-modal.dry-run-step'), index: Step.DryRun, active: false, icon: 'fa-solid fa-gears'},
    {title: translate('import-cbl-modal.final-import-step'), index: Step.Finalize, active: false, icon: 'fa-solid fa-floppy-disk'},
  ];
  currentStepIndex = this.steps[0].index;

  filesToProcess: Array<FileStep> = [];
  failedFiles: Array<FileStep> = [];


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



  nextStep() {
    if (this.currentStepIndex === Step.Import && !this.isFileSelected()) return;

    this.isLoading = true;
    switch (this.currentStepIndex) {
      case Step.Import:
        const files = this.uploadForm.get('files')?.value;
        if (!files) {
          this.toastr.error(translate('toasts.select-files-warning'));
          return;
        }
        // Load each file into filesToProcess and group their data
        const pages = [];
        for (let i = 0; i < files.length; i++) {
          const formData = new FormData();
          formData.append('cbl', files[i]);
          formData.append('dryRun', 'true');
          formData.append('comicVineMatching', this.cblSettingsForm.get('comicVineMatching')?.value + '');
          pages.push(this.readingListService.validateCbl(formData, true, this.cblSettingsForm.get('comicVineMatching')?.value as boolean));
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

    const pages = [];
    for (let i = 0; i < files.length; i++) {
      const formData = new FormData();
      formData.append('cbl', files[i]);
      formData.append('dryRun', 'true');
      formData.append('comicVineMatching', this.cblSettingsForm.get('comicVineMatching')?.value + '');
      pages.push(this.readingListService.importCbl(formData, true, this.cblSettingsForm.get('comicVineMatching')?.value as boolean));
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
      formData.append('comicVineMatching', this.cblSettingsForm.get('comicVineMatching')?.value + '');
      pages.push(this.readingListService.importCbl(formData, false, this.cblSettingsForm.get('comicVineMatching')?.value as boolean));
    }

    forkJoin(pages).subscribe(results => {
      results.forEach(cblImport => {
        const index = this.filesToProcess.findIndex(p => p.fileName === cblImport.fileName);
        this.filesToProcess[index].finalizeSummary = cblImport;
      });

      this.isLoading = false;
      this.currentStepIndex++;
      this.toastr.success(translate('toasts.reading-list-imported'));
      this.cdRef.markForCheck();
    });
  }
}
