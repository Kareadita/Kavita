import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { MangaReaderRoutingModule } from './manga-reader.router.module';
import { NgxSliderModule } from 'ngx-slider-v2';
import { InfiniteScrollerComponent } from './_components/infinite-scroller/infinite-scroller.component';
import { FullscreenIconPipe } from './_pipes/fullscreen-icon.pipe';
import { ReaderModeIconPipe } from './_pipes/reader-mode-icon.pipe';
import { CanvasRendererComponent } from './_components/canvas-renderer/canvas-renderer.component';
import { SingleRendererComponent } from './_components/single-renderer/single-renderer.component';
import { DoubleRendererComponent } from './_components/double-renderer/double-renderer.component';
import { DoubleReverseRendererComponent } from './_components/double-reverse-renderer/double-reverse-renderer.component';
import { MangaReaderComponent } from './_components/manga-reader/manga-reader.component';
import { FittingIconPipe } from './_pipes/fitting-icon.pipe';
import { DoubleNoCoverRendererComponent } from './_components/double-renderer-no-cover/double-no-cover-renderer.component';
import { NgSwipeModule } from '../ng-swipe/ng-swipe.module';
import {SafeStylePipe} from "../pipe/safe-style.pipe";
import {LoadingComponent} from "../shared/loading/loading.component";

@NgModule({
  declarations: [
    MangaReaderComponent,
    InfiniteScrollerComponent,
    CanvasRendererComponent,
    SingleRendererComponent,
    DoubleRendererComponent,
    DoubleReverseRendererComponent,
    DoubleNoCoverRendererComponent,
  ],
  imports: [
    CommonModule,
    MangaReaderRoutingModule,
    ReactiveFormsModule,

    NgbDropdownModule,
    NgxSliderModule,

    NgSwipeModule,
    SafeStylePipe,
    FittingIconPipe,
    ReaderModeIconPipe,
    FullscreenIconPipe,
    LoadingComponent
  ],
  exports: [
    MangaReaderComponent
  ]
})
export class MangaReaderModule { }
