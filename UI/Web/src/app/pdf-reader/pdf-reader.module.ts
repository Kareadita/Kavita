import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PdfReaderComponent } from './pdf-reader/pdf-reader.component';
import { PdfReaderRoutingModule } from './pdf-reader.router.module';
import { NgxExtendedPdfViewerModule } from 'ngx-extended-pdf-viewer';



@NgModule({
  declarations: [
    PdfReaderComponent
  ],
  imports: [
    CommonModule,
    PdfReaderRoutingModule,
    NgxExtendedPdfViewerModule,
  ]
})
export class PdfReaderModule { }
