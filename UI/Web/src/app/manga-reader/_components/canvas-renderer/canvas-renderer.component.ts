import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { map, Observable, of, Subject, takeUntil, tap } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { LayoutMode } from '../../_models/layout-mode';
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

  @Input() readerSettings$!: Observable<ReaderSetting>;
  @Input() image$!: Observable<HTMLImageElement | null>;
  @Input() bookmark$!: Observable<number>;
  @Input() showClickOverlay$!: Observable<boolean>;
  @Input() imageFit$!: Observable<FITTING_OPTION>; 

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private readonly onDestroy = new Subject<void>();

  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  fit: FITTING_OPTION = FITTING_OPTION.ORIGINAL;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;
  layoutMode: LayoutMode = LayoutMode.Single;

  isLoading: boolean = false;
  canvasImage: HTMLImageElement | null = null;
  showClickOverlayClass$!: Observable<string>;
  darkenss$: Observable<string> = of('brightness(100%)');
  imageFitClass$!: Observable<string>;
  renderWithCanvas: boolean = false;
  


  constructor(private readonly cdRef: ChangeDetectorRef, private mangaReaderService: ManagaReaderService) { }

  ngOnInit(): void {
    this.readerSettings$.pipe(takeUntil(this.onDestroy), tap(value => {
      this.fit = value.fitting;
      this.pageSplit = value.pageSplit;
      this.layoutMode = value.layoutMode;
      const rerenderNeeded = this.pageSplit != value.pageSplit;
      this.pagingDirection = value.pagingDirection;
      if (rerenderNeeded) {
        this.reset();
      }
    })).subscribe(() => {});

    this.darkenss$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'), 
      takeUntil(this.onDestroy)
    );

    this.imageFitClass$ = this.imageFit$.pipe(
      takeUntil(this.onDestroy),
      map(fit => {
        if (
          this.canvasImage != null && 
          this.mangaReaderService.isWideImage(this.canvasImage) &&
          this.layoutMode === LayoutMode.Single &&
          fit !== FITTING_OPTION.WIDTH &&
          this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit)
          ) {
          // Rewriting to fit to width for this cover image
          return FITTING_OPTION.WIDTH;
        }
        return fit;
      })
    );


    this.bookmark$.pipe(
      takeUntil(this.onDestroy),
      tap(_ => {
        if (this.currentImageSplitPart === SPLIT_PAGE_PART.NO_SPLIT) return;
        if (!this.canvas) return;

        const elements = [this.canvas?.nativeElement];
        console.log('Applying bookmark on ', elements);
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe(() => {});

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''), 
      takeUntil(this.onDestroy)
    );
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
    this.renderWithCanvas = false;
    this.cdRef.markForCheck();
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.ctx || !this.canvas) return;
    this.canvasImage = img[0];

    this.renderWithCanvas = this.mangaReaderService.shouldSplit(this.canvasImage, this.pageSplit);
    
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
