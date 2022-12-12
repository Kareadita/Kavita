import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Observable, of, Subject, map, takeUntil, tap, zip, shareReplay, filter } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { ReaderService } from 'src/app/_services/reader.service';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { ManagaReaderService } from '../../_series/managa-reader.service';

/**
 * Renders 2 pages except on first page, last page, and before a wide image
 */
@Component({
  selector: 'app-double-renderer',
  templateUrl: './double-renderer.component.html',
  styleUrls: ['./double-renderer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DoubleRendererComponent implements OnInit, OnDestroy, ImageRenderer {

  @Input() readerSettings$!: Observable<ReaderSetting>;
  @Input() image$!: Observable<HTMLImageElement | null>;
  /**
   * The image fit class
   */
  @Input() imageFit$!: Observable<FITTING_OPTION>;  
  @Input() bookmark$!: Observable<number>;
  @Input() showClickOverlay$!: Observable<boolean>;
  @Input() pageNum$!: Observable<{pageNum: number, maxPages: number}>;

  @Input() getPage!: (pageNum: number) => HTMLImageElement;

  @Output() imageHeight: EventEmitter<number> = new EventEmitter<number>();

  imageFitClass$!: Observable<string>;
  showClickOverlayClass$!: Observable<string>;
  readerModeClass$!: Observable<string>;
  layoutClass$!: Observable<string>;
  shouldRenderSecondPage$!: Observable<boolean>;
  darkenss$: Observable<string> = of('brightness(100%)');
  layoutMode: LayoutMode = LayoutMode.Single;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;
  pageNum: number = 0;
  maxPages: number = 0;

  /**
   * Used to render a page on the canvas or in the image tag. This Image element is prefetched by the cachedImages buffer.
   * @remarks Used for rendering to screen.
   */
  currentImage = new Image();
   /**
    * Used solely for LayoutMode.Double rendering. 
    * @remarks Used for rendering to screen.
    */
  currentImage2 = new Image();
   /**
    * Used solely for LayoutMode.Double rendering. Will always hold the previous image to currentImage
    * @see currentImage
    */
  currentImagePrev = new Image();
   /**
    * Used solely for LayoutMode.Double rendering. Will always hold the next image to currentImage
    * @see currentImage
    */
  currentImageNext = new Image();
  /**
    * Used solely for LayoutMode.Double rendering. Will always hold the current - 2 image to currentImage
    * @see currentImage
    */
  currentImage2Behind = new Image();
  /**
   * Used solely for LayoutMode.Double rendering. Will always hold the current + 2 image to currentImage
   * @see currentImage
   */
  currentImage2Ahead = new Image();

  /**
   * Determines if we should render a double page.
   * The general gist is if we are on double layout mode, the current page (first page) is not a cover image or a wide image 
   * and the next page is not a wide image (as only non-wides should be shown next to each other).
   * @remarks This will always fail if the window's width is greater than the height
  */
  shouldRenderDouble$!: Observable<boolean>;

  private readonly onDestroy = new Subject<void>();

  get ReaderMode() {return ReaderMode;} 
  get FITTING_OPTION() {return FITTING_OPTION;} 
  get LayoutMode() {return LayoutMode;} 

  

  constructor(private readonly cdRef: ChangeDetectorRef, public mangaReaderService: ManagaReaderService, 
    @Inject(DOCUMENT) private document: Document, public readerService: ReaderService) { }

  ngOnInit(): void {
    this.readerModeClass$ = this.readerSettings$.pipe(
      filter(_ => this.isValid()),
      map(values => values.readerMode), 
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      takeUntil(this.onDestroy)
    );

    this.darkenss$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'), 
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''), 
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      tap(pageInfo => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;

        this.currentImage = this.getPage(this.pageNum);
        this.currentImage2 = this.getPage(this.pageNum + 1);

        this.currentImageNext = this.getPage(this.pageNum + 1);
        this.currentImagePrev = this.getPage(this.pageNum - 1);

        this.currentImage2Behind = this.getPage(this.pageNum - 2);
        this.currentImage2Ahead = this.getPage(this.pageNum + 2);
        this.cdRef.markForCheck();
      })).subscribe(() => {});

    this.shouldRenderDouble$ = this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      map((_) => this.shouldRenderDouble())
    );

    this.layoutClass$ = zip(this.shouldRenderDouble$, this.imageFit$).pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      map((value) =>  {
        if (!value[0]) return 'd-none';
        if (value[0] && value[1] === FITTING_OPTION.WIDTH) return 'fit-to-width-double-offset';
        if (value[0] && value[1] === FITTING_OPTION.HEIGHT) return 'fit-to-height-double-offset';
        if (value[0] && value[1] === FITTING_OPTION.ORIGINAL) return 'original-double-offset';
        return '';
      })
    );

    this.shouldRenderSecondPage$ = this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      map(_ => {
        return this.shouldRenderDouble();
        // if (this.currentImage2.src === '') {
        //   console.log('Not rendering second page as 2nd image is empty');
        //   return false;
        // }
        // if (this.mangaReaderService.isCoverImage(this.pageNum)) {
        //   console.log('Not rendering second page as on cover image');
        //   return false;
        // }
        // if (this.readerService.imageUrlToPageNum(this.currentImage2.src) > this.maxPages - 1) {
        //   console.log('Not rendering second page as 2nd image is on last page');
        //   return false;
        // }
        // if (this.mangaReaderService.isWideImage(this.currentImageNext)) {
        //   console.log('Not rendering second page as next page is wide');
        //   return false;
        // }

        // if (this.mangaReaderService.isWideImage(this.currentImage)) {
        //   console.log('Not rendering second page as next page is wide');
        //   return false;
        // }

        // if (this.mangaReaderService.isWideImage(this.currentImagePrev)) {
        //   console.log('Not rendering second page as prev page is wide');
        //   return false;
        // }
        // if (this.mangaReaderService.isCoverImage(this.pageNum)) {
        //   console.log('Not rendering double as current page is cover image');
        //   return false;
        // }
    
        // if (this.mangaReaderService.isWideImage(this.currentImage) || this.mangaReaderService.isWidePage(this.pageNum) ) {
        //   console.log('Not rendering double as current page is wide image');
        //   return false;
        // }
    
        // //  && this.maxPages % 2 !== 0 We can check if we have an odd number of pairs
        // if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
        //   console.log('Not rendering double as current page is last and there are an odd number of pages');
        //   return false;
        // }
    
        // if (this.mangaReaderService.isWidePage(this.pageNum + 1) ) {
        //   console.log('Not rendering double as next page is wide image');
        //   return false;
        // }
        // return true;
      })
    );

    this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      tap(values => {
        this.layoutMode = values.layoutMode;
        this.pageSplit = values.pageSplit;
        this.cdRef.markForCheck();
      })
    ).subscribe(() => {});

    this.bookmark$.pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);

        const image2 = this.document.querySelector('#image-2');
        if (image2 != null) elements.push(image2);
  
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe(() => {});


    this.imageFitClass$ = this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
      map(values => values.fitting),
      shareReplay()
    );
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  shouldRenderDouble() {
    if (this.layoutMode !== LayoutMode.Double) return false;


    // If we are cover image, a wide image or last page, we don't render double
  

    if (this.mangaReaderService.isCoverImage(this.pageNum)) {
      console.log('Not rendering double as current page is cover image');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum) ) {
      console.log('Not rendering double as current page is wide image');
      return false;
    }

    //  && this.maxPages % 2 !== 0 We can check if we have an odd number of pairs
    if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
      console.log('Not rendering double as current page is last and there are an odd number of pages');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum + 1) ) {
      console.log('Not rendering double as next page is wide image');
      return false;
    }

    return true;
  }

  isValid() {
    return this.layoutMode === LayoutMode.Double;
  }
  
  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;

    console.log('[DoubleRenderer] renderPage(): ', this.pageNum);
    console.log(this.readerService.imageUrlToPageNum(this.currentImage2Behind.src), this.readerService.imageUrlToPageNum(this.currentImagePrev.src),
    '[', this.readerService.imageUrlToPageNum(this.currentImage.src), ']',
    this.readerService.imageUrlToPageNum(this.currentImageNext.src), this.readerService.imageUrlToPageNum(this.currentImage2Ahead.src))
    
    // First load, switching from double manga -> double, this is 0 and thus not rendering
    if (!this.shouldRenderDouble() && (this.currentImage.height || img[0].height) > 0) {
      this.imageHeight.emit(this.currentImage.height || img[0].height);
      return;
    }
    
    this.currentImage2 = this.currentImageNext;

    this.cdRef.markForCheck();
    this.imageHeight.emit(Math.max(this.currentImage.height, this.currentImage2.height));
    this.cdRef.markForCheck();
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(direction: PAGING_DIRECTION): number {
    if (this.layoutMode !== LayoutMode.Double) return 0;

    // console.log('[getPageAmount for double reverse]: ', allImages.map(img => {
    //   const page = this.readerService.imageUrlToPageNum(img.src);
    //   if (page === this.pageNum) return '[' + page;
    //   if (page === this.pageNum + 1) return page + ']';
    //   return page + '';
    // }));
    console.log("Current Page: ", this.pageNum);
    console.log("Total Pages: ", this.maxPages);

    switch (direction) {
      case PAGING_DIRECTION.FORWARD:
        if (this.mangaReaderService.isCoverImage(this.pageNum)) {
          console.log('Moving forward 1 page as on cover image');
          return 1;
        }
        if (this.mangaReaderService.isWideImage(this.currentImage)) {
          console.log('Moving forward 1 page as current page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWideImage(this.currentImageNext)) {
          console.log('Moving forward 1 page as next page is wide');
          return 1;
        }
        if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
          console.log('Moving forward 1 page as 2 pages left');
          return 1;
        }
        if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
          console.log('Moving forward 1 page as 1 page left');
          return 1;
        }
        console.log('Moving forward 2 pages');
        return 2;
      case PAGING_DIRECTION.BACKWARDS:
        if (this.mangaReaderService.isCoverImage(this.pageNum)) {
          console.log('Moving back 1 page as on cover image');
          return 1;
        }
        if (this.mangaReaderService.isWideImage(this.currentImage)) {
          console.log('Moving back 1 page as current page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWideImage(this.currentImagePrev)) {
          console.log('Moving back 1 page as prev page is wide');
          return 1;
        }
        // ?! On fresh load, there is a timing issue with this. 
        if (this.mangaReaderService.isWideImage(this.currentImage2Behind)) {
          console.log('Moving back 1 page as 2 pages back is wide');
          return 1;
        }
        // Not sure about this condition on moving backwards
        // if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
        //   console.log('Moving back 1 page as 2 pages left');
        //   return 1;
        // }
        console.log('Moving back 2 pages');
        return 2;
    }
  }
  reset(): void {}

}
