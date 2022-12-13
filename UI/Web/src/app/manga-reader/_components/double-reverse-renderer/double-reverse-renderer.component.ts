import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Observable, of, Subject, map, takeUntil, tap, zip, shareReplay, filter, combineLatest } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { ReaderService } from 'src/app/_services/reader.service';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { ManagaReaderService } from '../../_series/managa-reader.service';

/**
 * This is aimed at manga. Double page renderer but where if we have page = 10, you will see
 * page 11 page 10. 
 */
@Component({
  selector: 'app-double-reverse-renderer',
  templateUrl: './double-reverse-renderer.component.html',
  styleUrls: ['./double-reverse-renderer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DoubleReverseRendererComponent implements OnInit, OnDestroy, ImageRenderer {


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
  darkenss$: Observable<string> = of('brightness(100%)');
  emulateBookClass$: Observable<string> = of('');
  layoutMode: LayoutMode = LayoutMode.Single;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;
  pageNum: number = 0;
  maxPages: number = 0;

  /**
   * Used to render a page on the canvas or in the image tag. This Image element is prefetched by the cachedImages buffer.
   * @remarks Used for rendering to screen.
   */
  leftImage = new Image();
   /**
    * Used solely for LayoutMode.Double rendering. 
    * @remarks Used for rendering to screen.
    */
  rightImage = new Image();
   /**
    * Used solely for LayoutMode.Double rendering. Will always hold the next image to currentImage
    * @see currentImage
    */
  currentImageNext = new Image();

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

    this.emulateBookClass$ = this.readerSettings$.pipe(
      map(data => data.emulateBook),
      map(enabled => enabled ? 'book-shadow' : ''), 
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
      tap(pageInfo => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;

        this.leftImage = this.getPage(this.pageNum);
        this.rightImage = this.getPage(this.pageNum + 1);

        this.currentImageNext = this.getPage(this.pageNum + 1);
      }),
      filter(_ => this.isValid()),
    ).subscribe(() => {});

    this.shouldRenderDouble$ = this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      map((_) => this.shouldRenderDouble()),
      filter(_ => this.isValid()),
      shareReplay()
    );

    this.layoutClass$ = combineLatest([this.shouldRenderDouble$, this.imageFit$]).pipe(
      takeUntil(this.onDestroy),
      map((value) =>  {
        if (!value[0]) return 'd-none';
        if (value[0] && value[1] === FITTING_OPTION.WIDTH) return 'fit-to-width-double-offset';
        if (value[0] && value[1] === FITTING_OPTION.HEIGHT) return 'fit-to-height-double-offset';
        if (value[0] && value[1] === FITTING_OPTION.ORIGINAL) return 'original-double-offset';
        return '';
      }),
      filter(_ => this.isValid()),
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
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);

        const image2 = this.document.querySelector('#image-2');
          if (image2 != null) elements.push(image2);

        this.mangaReaderService.applyBookmarkEffect(elements);
      }),
      filter(_ => this.isValid()),
    ).subscribe(() => {});


    this.imageFitClass$ = this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      map(values => values.fitting),
      filter(_ => this.isValid()),
      shareReplay()
    );
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  shouldRenderDouble() {
    if (!this.isValid()) return false;

    if (this.mangaReaderService.isCoverImage(this.pageNum)) {
      console.log('Not rendering double as current page is cover image');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum)) {
      console.log('Not rendering double as current page is wide image');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum + 1) ) {
      console.log('Not rendering double as next page is wide image');
      return false;
    }

    if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
      console.log('Not rendering double as current page is last and there are an odd number of pages');
      return false;
    }

    return true;
  }

  isWide(img: HTMLImageElement) {
    const page = this.readerService.imageUrlToPageNum(img.src);
    return this.mangaReaderService.isWidePage(page);
  }

  isValid() {
    return this.layoutMode === LayoutMode.DoubleReversed;
  }
  
  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;
    
    this.rightImage = this.currentImageNext;
    this.cdRef.markForCheck();

    this.imageHeight.emit(Math.max(this.leftImage.height, this.rightImage.height));
    this.cdRef.markForCheck();
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(direction: PAGING_DIRECTION): number {
    if (this.layoutMode !== LayoutMode.DoubleReversed) return 0;

    switch (direction) {
      case PAGING_DIRECTION.FORWARD:
        if (this.mangaReaderService.isCoverImage(this.pageNum)) {
          console.log('Moving forward 1 page as on cover image');
          return 1;
        }

        if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
          console.log('Moving forward 1 page as 2 pages left');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum)) {
          console.log('Moving forward 1 page as current page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum + 1)) {
          console.log('Moving forward 1 page as current page is wide');
          return 1;
        }

        if (this.mangaReaderService.isLastImage(this.pageNum + 1, this.maxPages)) {
          console.log('Moving forward 2 pages as right image is the last page and we just rendered double page');
          return 2;
        }

        if (this.pageNum === this.maxPages - 1) {
          console.log('Moving forward 0 page as on last page');
          return 0;
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

        if (this.mangaReaderService.isWidePage(this.pageNum + 1)) {
          console.log('Moving back 2 page as right page is wide');
          return 2;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum + 2)) {
          console.log('Moving back 1 page as coming from wide page');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum)) {
          console.log('Moving back 1 page as left page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum) && (!this.mangaReaderService.isWidePage(this.pageNum - 4))) {
          console.log('Moving back 1 page as left page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum - 1)) {
          console.log('Moving back 1 page as prev page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum - 2)) {
          console.log('Moving back 1 page as 2 pages back is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum + 2)) {
          console.log('Moving back 2 page as 2 pages back is wide');
          return 1;
        }
        // Not sure about this condition on moving backwards
        if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
          console.log('Moving back 1 page as 2 pages left');
          return 1;
        }
        console.log('Moving back 2 pages');
        return 2;
    }
  }
  reset(): void {}


}
