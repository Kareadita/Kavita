import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild } from '@angular/core';
import { Observable, Subject, tap } from 'rxjs';
import { FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART } from '../_models/reader-enums';
import { ReaderSetting } from '../_models/reader-setting';

@Component({
  selector: 'app-canvas-renderer',
  templateUrl: './canvas-renderer.component.html',
  styleUrls: ['./canvas-renderer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CanvasRendererComponent implements OnInit, AfterViewInit, OnDestroy {

  @Input() canvasImage: HTMLImageElement = new Image();
  @Input() fittingClass: string = '';
  @Input() readerSettings!: Observable<ReaderSetting>;

  @Input() image!: Observable<ReaderSetting>;

  @Output() render = new EventEmitter<HTMLImageElement>();


  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private readonly onDestroy = new Subject<void>();

  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  isSplitLeftToRight: boolean = false;
  isWideImage: boolean = false;
  isNoSplit: boolean = false;
  fit: FITTING_OPTION = FITTING_OPTION.ORIGINAL;

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.readerSettings.pipe(tap(value => {
      this.isNoSplit = value.isNoSplit;
      this.fit = value.fitting;
      this.isSplitLeftToRight = value.isSplitLeftToRight;
      this.isWideImage = value.isWideImage;
      this.isNoSplit = value.isNoSplit;
      console.log('Canvas Renderer - settings: ', value);
    })).subscribe(() => {});
  }

  ngAfterViewInit() {
    if (this.canvas) {
      this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });
      this.canvasImage.onload = () => this.renderPage();
    }
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }



  updateSplitPage() {
    const needsSplitting = this.isWideImage;
    if (!needsSplitting || this.isNoSplit) {
      this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
      return;
    }

    if (this.pagingDirection === PAGING_DIRECTION.FORWARD) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = this.isSplitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const r2lSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : r2lSplittingPart;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight ? l2rSplittingPart : SPLIT_PAGE_PART.LEFT_PART;
          break;
      }
    } else if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = this.isSplitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight ? l2rSplittingPart : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = this.isSplitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          break;
      }
    }
  }


  renderPage() {

    if (!this.ctx || !this.canvas) return;

    const needsSplitting = this.isWideImage;
    this.updateSplitPage();

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
      // We can emit an event instead when renderer renders
      this.render.emit(this.canvasImage);
    }
    //this.isLoading = false;
    this.cdRef.markForCheck();
  }




  /**
   * There are some hard limits on the size of canvas' that we must cap at. https://github.com/jhildenbiddle/canvas-size#test-results
   * For Safari, it's 16,777,216, so we cap at 4096x4096 when this happens. The drawImage in render will perform bi-cubic scaling for us.
   * @returns If we should continue to the render loop
   */
   setCanvasSize() {
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
