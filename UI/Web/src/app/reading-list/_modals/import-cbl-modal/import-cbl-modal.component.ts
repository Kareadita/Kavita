import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { CblImportSummary } from 'src/app/_models/reading-list/cbl/cbl-import-summary';
import { ReadingListService } from 'src/app/_services/reading-list.service';

@Component({
  selector: 'app-import-cbl-modal',
  templateUrl: './import-cbl-modal.component.html',
  styleUrls: ['./import-cbl-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportCblModalComponent {

  @ViewChild('fileUpload') fileUpload!: ElementRef<HTMLInputElement>;

  acceptableExtensions: string = ['.cbl'].join(',');;
  importSummaries: Array<CblImportSummary> = [];

  get Breakpoint() { return Breakpoint; }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef) {}

  close() {
    this.ngModal.close();
  }

  onFileSelected(event: any) {
    console.log('event: ', event);
    if (!(event.target as HTMLInputElement).files === null || (event.target as HTMLInputElement).files?.length === 0) return;

    const file = (event.target as HTMLInputElement).files![0];

    if (file) {

        //this.fileName = file.name;

        const formData = new FormData();

        formData.append("cbl", file);

        this.readingListService.importCbl(formData).subscribe(res => {
          this.importSummaries.push(res);
          this.cdRef.markForCheck();
        });
        this.fileUpload.value = '';
    }
}
}
