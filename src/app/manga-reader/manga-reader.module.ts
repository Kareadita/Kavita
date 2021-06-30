import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MangaReaderComponent } from './manga-reader.component';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbButtonsModule, NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { MangaReaderRoutingModule } from './manga-reader.router.module';
import { SharedModule } from '../shared/shared.module';
import { NgxSliderModule } from '@angular-slider/ngx-slider';
import { InfiniteScrollerComponent } from './infinite-scroller/infinite-scroller.component';

@NgModule({
  declarations: [
    MangaReaderComponent,
    InfiniteScrollerComponent
  ],
  imports: [
    CommonModule,
    MangaReaderRoutingModule,
    ReactiveFormsModule,

    NgbButtonsModule,
    NgbDropdownModule,
    NgxSliderModule,
    SharedModule,
  ],
  exports: [
    MangaReaderComponent
  ]
})
export class MangaReaderModule { }
