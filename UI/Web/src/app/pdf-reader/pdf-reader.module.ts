import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PdfReaderComponent } from './pdf-reader/pdf-reader.component';
import { PdfReaderRoutingModule } from './pdf-reader.router.module';
import { NgxExtendedPdfViewerModule } from 'ngx-extended-pdf-viewer';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';



@NgModule({
  declarations: [
    PdfReaderComponent
  ],
  imports: [
    CommonModule,
    PdfReaderRoutingModule,
    NgxExtendedPdfViewerModule,
    NgbTooltipModule
  ]
})
export class PdfReaderModule { }
