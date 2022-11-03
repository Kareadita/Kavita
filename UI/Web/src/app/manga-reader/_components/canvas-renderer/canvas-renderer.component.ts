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
export class CanvasRendererComponent implements OnInit, AfterViewInit, OnDestroy {

  canvasImage: HTMLImageElement | null = null;
  /**
   * The current page
   */
  @Input() pageNum!: Observable<number>;

  //@Input() fittingClass: string = ''; // I don't think we need this
  /**
   * When rendering settings update, this observable will keep track so canvas renderer is kept in sync
   */
  @Input() readerSettings!: Observable<ReaderSetting>;

  // The idea here is for image to flow in via observable and when that thappens, we just render onto the canvas
  @Input() image!: Observable<HTMLImageElement | null>;

  @Output() render = new EventEmitter<HTMLImageElement>();


  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private readonly onDestroy = new Subject<void>();

  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  fit: FITTING_OPTION = FITTING_OPTION.ORIGINAL;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;

  isLoading: boolean = false;


  constructor(private readonly cdRef: ChangeDetectorRef, private mangaReaderService: ManagaReaderService) { }

  ngOnInit(): void {
    this.readerSettings.pipe(tap(value => {
      this.fit = value.fitting;
      this.pageSplit = value.pageSplit;
      this.pagingDirection = value.pagingDirection;
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
      //if (this.canvasImage) this.renderPage();
      if (this.canvasImage) this.canvasImage.onload = () => this.renderPage();
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
  renderPage(img: HTMLImageElement | null = this.canvasImage) {
    console.log('[Canvas Renderer] RenderPage() started', this.canvasImage);
    if (img) {
      this.canvasImage = img;
    }
    
    if (this.canvasImage == null) return;
    if (!this.ctx || !this.canvas) return;
    
    // ?! Bug: updating from no split -> left to right will render right side first (likely has to do with paging direction)
    const needsSplitting = this.updateSplitPage();
    console.log('\tSplit Part: ', this.currentImageSplitPart);

    if (!needsSplitting) {
      return;
    }

    if (this.currentImageSplitPart === SPLIT_PAGE_PART.NO_SPLIT) return;

    this.isLoading = true;
    this.setCanvasSize();

    // console.log('\tAttempting to render page: ', this.canvasImage.src);
    // console.log('\tPage loaded: ', this.canvasImage.complete);
    // console.log('\tNeeds Splitting: ', needsSplitting);
    console.log('\tCurrent Split Part: ', this.currentImageSplitPart);

    if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    } else if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    }

    // Reset scroll on non HEIGHT Fits
    if (this.fit !== FITTING_OPTION.HEIGHT) {
      //this.readingArea.nativeElement.scroll(0,0);
      // We can emit an event instead when renderer renders so the parent can do any extra work, like scrolling
      this.render.emit(this.canvasImage);
    }

    this.isLoading = false;
    this.cdRef.markForCheck();
    console.log('[Canvas Renderer] RenderPage() ended');
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
