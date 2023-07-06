import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookReaderComponent } from './_components/book-reader/book-reader.component';
import { BookReaderRoutingModule } from './book-reader.router.module';
import { SafeStylePipe } from './_pipes/safe-style.pipe';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbAccordionModule, NgbNavModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReaderSettingsComponent } from './_components/reader-settings/reader-settings.component';
import { TableOfContentsComponent } from './_components/table-of-contents/table-of-contents.component';
import {DrawerComponent} from "../shared/drawer/drawer.component";


@NgModule({
    imports: [
        CommonModule,
        BookReaderRoutingModule,
        ReactiveFormsModule,
        NgbProgressbarModule,
        NgbTooltipModule,
        NgbTooltipModule,
        NgbAccordionModule,
        NgbNavModule,
        DrawerComponent,
        BookReaderComponent, SafeStylePipe, TableOfContentsComponent, ReaderSettingsComponent,
    ], exports: [
        BookReaderComponent,
        SafeStylePipe
    ]
})
export class BookReaderModule { }
