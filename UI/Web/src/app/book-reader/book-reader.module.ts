import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookReaderComponent } from './book-reader/book-reader.component';
import { BookReaderRoutingModule } from './book-reader.router.module';
import { SharedModule } from '../shared/shared.module';
import { SafeStylePipe } from './safe-style.pipe';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';


@NgModule({
  declarations: [BookReaderComponent, SafeStylePipe],
  imports: [
    CommonModule,
    BookReaderRoutingModule,
    ReactiveFormsModule,
    SharedModule,
    NgbProgressbarModule,
    NgbTooltipModule
  ], exports: [
    BookReaderComponent,
    SafeStylePipe
  ]
})
export class BookReaderModule { }
