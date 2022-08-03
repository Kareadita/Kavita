import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MangaReaderComponent } from './manga-reader.component';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { MangaReaderRoutingModule } from './manga-reader.router.module';
import { SharedModule } from '../shared/shared.module';
import { NgxSliderModule } from '@angular-slider/ngx-slider';
import { InfiniteScrollerComponent } from './infinite-scroller/infinite-scroller.component';
import { ReaderSharedModule } from '../reader-shared/reader-shared.module';
import { FullscreenIconPipe } from './fullscreen-icon.pipe';
import { PipeModule } from '../pipe/pipe.module';

@NgModule({
  declarations: [
    MangaReaderComponent,
    InfiniteScrollerComponent,
    FullscreenIconPipe
  ],
  imports: [
    CommonModule,
    MangaReaderRoutingModule,
    ReactiveFormsModule,
    PipeModule,

    NgbDropdownModule,
    NgxSliderModule,
    SharedModule,
    ReaderSharedModule,
  ],
  exports: [
    MangaReaderComponent
  ]
})
export class MangaReaderModule { }
