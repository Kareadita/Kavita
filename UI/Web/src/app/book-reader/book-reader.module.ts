import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookReaderComponent } from './_components/book-reader/book-reader.component';
import { BookReaderRoutingModule } from './book-reader.router.module';
import { SharedModule } from '../shared/shared.module';
import { SafeStylePipe } from './_pipes/safe-style.pipe';
import { ReactiveFormsModule } from '@angular/forms';
import { PipeModule } from '../pipe/pipe.module';
import { NgbAccordionModule, NgbNavModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReaderSettingsComponent } from './_components/reader-settings/reader-settings.component';
import { TableOfContentsComponent } from './_components/table-of-contents/table-of-contents.component';


@NgModule({
  declarations: [BookReaderComponent, SafeStylePipe, TableOfContentsComponent, ReaderSettingsComponent],
  imports: [
    CommonModule,
    BookReaderRoutingModule,
    ReactiveFormsModule,
    SharedModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    PipeModule,
    NgbTooltipModule,
    NgbAccordionModule, // Drawer
    NgbNavModule, // Drawer
  ], exports: [
    BookReaderComponent,
    SafeStylePipe
  ]
})
export class BookReaderModule { }
