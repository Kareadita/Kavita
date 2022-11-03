import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild } from '@angular/core';
import { distinctUntilChanged, filter, Observable, of, Subject, switchMap, tap } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { ManagaReaderService } from '../../_series/managa-reader.service';

@Component({
  selector: 'app-canvas-renderer',
  templateUrl: './canvas-renderer.component.html',
  styleUrls: ['./canvas-renderer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CanvasRendererComponent implements OnInit, AfterViewInit, OnDestroy, ImageRenderer {

  @Input() readerSettings!: Observable<ReaderSetting>;
  @Input() image!: Observable<HTMLImageElement | null>;

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private readonly onDestroy = new Subject<void>();

  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  fit: FITTING_OPTION = FITTING_OPTION.ORIGINAL;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;

  isLoading: boolean = false;
  canvasImage: HTMLImageElement | null = null;


  constructor(private readonly cdRef: ChangeDetectorRef, private mangaReaderService: ManagaReaderService) { }

  ngOnInit(): void {
    this.readerSettings.pipe(tap(value => {
      this.fit = value.fitting;
      this.pageSplit = value.pageSplit;
      const rerenderNeeded = this.pageSplit != value.pageSplit;
      this.pagingDirection = value.pagingDirection;
      if (rerenderNeeded) {
        this.reset();
      }

    })).subscribe(() => {});

    

    // this.image.pipe(
    //   tap(img => console.log('[Canvas Renderer] image update: ', img)),
    //   distinctUntilChanged(), 
    //   tap(img => {
    //     if (img === null) return;
    //     this.canvasImage = img;
    //     this.canvasImage.addEventListener('load', () => {
    //       this.renderPage();
    //     }, false);
    //     this.cdRef.markForCheck();
    //   })
    // ).subscribe(() => {});
  }

  ngAfterViewInit() {
    if (this.canvas) {
      this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });
    }
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  reset() {
    this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
  }

  updateSplitPage() {
    if (this.canvasImage == null) return;
    const needsSplitting = this.mangaReaderService.isWideImage(this.canvasImage);
    if (!needsSplitting || this.mangaReaderService.isNoSplit(this.pageSplit)) {
      this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
      return needsSplitting;
    }
    const splitLeftToRight = this.mangaReaderService.isSplitLeftToRight(this.pageSplit);

    if (this.pagingDirection === PAGING_DIRECTION.FORWARD) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const r2lSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : r2lSplittingPart;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight ? l2rSplittingPart : SPLIT_PAGE_PART.LEFT_PART;
          break;
      }
    } else if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight? l2rSplittingPart : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          break;
      }
    }
    return needsSplitting;
  }

  /**
   * This renderer does not render when splitting is not needed
   * @param img 
   * @returns 
   */
  renderPage(img: Array<HTMLImageElement | null>) {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.ctx || !this.canvas) return;
    this.canvasImage = img[0];
    
    const needsSplitting = this.updateSplitPage();
    if (!needsSplitting) return;
    if (this.currentImageSplitPart === SPLIT_PAGE_PART.NO_SPLIT) return;

    this.isLoading = true;
    this.setCanvasSize();

    if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    } else if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    }

    this.isLoading = false;
    this.cdRef.markForCheck();
  }

  getPageAmount() {
    if (this.canvasImage === null) return 1;
    if (!this.mangaReaderService.isWideImage(this.canvasImage)) return 1;
    switch(this.pagingDirection) {
      case PAGING_DIRECTION.FORWARD:
        return this.shouldMoveNext() ? 1 : 0;
      case PAGING_DIRECTION.BACKWARDS:
        return this.shouldMovePrev() ? 1 : 0;
    }
  }

  shouldMoveNext() {
    if (this.mangaReaderService.isNoSplit(this.pageSplit)) return true;
    return this.currentImageSplitPart !== (this.mangaReaderService.isSplitLeftToRight(this.pageSplit) ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART);
  }

  shouldMovePrev() {
    if (this.mangaReaderService.isNoSplit(this.pageSplit)) return true;
    return this.currentImageSplitPart !== (this.mangaReaderService.isSplitLeftToRight(this.pageSplit) ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART);
  }

  /**
   * There are some hard limits on the size of canvas' that we must cap at. https://github.com/jhildenbiddle/canvas-size#test-results
   * For Safari, it's 16,777,216, so we cap at 4096x4096 when this happens. The drawImage in render will perform bi-cubic scaling for us.
   */
   setCanvasSize() {
    if (this.canvasImage == null) return;
    if (!this.ctx || !this.canvas) { return; }
    // TODO: Move this somewhere else (maybe canvas renderer?)
    // eslint-disable-next-line @typescript-eslint/ban-ts-comment
    // @ts-ignore
    const isSafari = [
      'iPad Simulator',
      'iPhone Simulator',
      'iPod Simulator',
      'iPad',
      'iPhone',
      'iPod'
    ].includes(navigator.platform)
    // iPad on iOS 13 detection
    || (navigator.userAgent.includes("Mac") && "ontouchend" in document);
    const canvasLimit = isSafari ? 16_777_216 : 124_992_400;
    const needsScaling = this.canvasImage.width * this.canvasImage.height > canvasLimit;
    if (needsScaling) {
      this.canvas.nativeElement.width = isSafari ? 4_096 : 16_384;
      this.canvas.nativeElement.height = isSafari ? 4_096 : 16_384;
    } else {
      this.canvas.nativeElement.width = this.canvasImage.width;
      this.canvas.nativeElement.height = this.canvasImage.height;
    }
    this.cdRef.markForCheck();
  }
}
