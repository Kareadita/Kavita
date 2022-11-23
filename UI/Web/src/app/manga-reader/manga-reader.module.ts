import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { MangaReaderRoutingModule } from './manga-reader.router.module';
import { SharedModule } from '../shared/shared.module';
import { NgxSliderModule } from '@angular-slider/ngx-slider';
import { InfiniteScrollerComponent } from './_components/infinite-scroller/infinite-scroller.component';
import { ReaderSharedModule } from '../reader-shared/reader-shared.module';
import { PipeModule } from '../pipe/pipe.module';
import { FullscreenIconPipe } from './_pipes/fullscreen-icon.pipe';
import { LayoutModeIconPipe } from './_pipes/layout-mode-icon.pipe';
import { ReaderModeIconPipe } from './_pipes/reader-mode-icon.pipe';
import { SwipeDirective } from './swipe.directive';
import { CanvasRendererComponent } from './_components/canvas-renderer/canvas-renderer.component';
import { SingleRendererComponent } from './_components/single-renderer/single-renderer.component';
import { DoubleRendererComponent } from './_components/double-renderer/double-renderer.component';
import { DoubleReverseRendererComponent } from './_components/double-reverse-renderer/double-reverse-renderer.component';
import { MangaReaderComponent } from './_components/manga-reader/manga-reader.component';

@NgModule({
  declarations: [
    MangaReaderComponent,
    InfiniteScrollerComponent,
    FullscreenIconPipe,
    ReaderModeIconPipe,
    LayoutModeIconPipe,
    SwipeDirective,
    CanvasRendererComponent,
    SingleRendererComponent,
    DoubleRendererComponent,
    DoubleReverseRendererComponent,
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
