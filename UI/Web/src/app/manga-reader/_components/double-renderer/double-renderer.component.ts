import { DOCUMENT } from '@angular/common';
import { ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Observable, of, Subject, map, takeUntil, tap, merge, zip, take } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { ReaderService } from 'src/app/_services/reader.service';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { ManagaReaderService } from '../../_series/managa-reader.service';

@Component({
  selector: 'app-double-renderer',
  templateUrl: './double-renderer.component.html',
  styleUrls: ['./double-renderer.component.scss']
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
      map(values => values.readerMode), 
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      takeUntil(this.onDestroy)
    );

    this.darkenss$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'), 
      takeUntil(this.onDestroy)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''), 
      takeUntil(this.onDestroy)
    );

    this.shouldRenderDouble$ = this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      map((pageInfo) => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;
        if (this.layoutMode !== LayoutMode.Double) return false;

        return !(
          this.mangaReaderService.isCoverImage(this.pageNum)
          || this.mangaReaderService.isWideImage(this.currentImage)
          || this.mangaReaderService.isWideImage(this.currentImageNext)
          );
      })
    );

    this.layoutClass$ = zip(this.shouldRenderDouble$, this.imageFit$).pipe(
      takeUntil(this.onDestroy),
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
      map(_ => {
        return (this.currentImage2.src !== '') 
              && (this.readerService.imageUrlToPageNum(this.currentImage2.src) <= this.maxPages - 1 
              && !this.mangaReaderService.isCoverImage(this.pageNum));
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
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);

        if (this.layoutMode !== LayoutMode.Single) {
          const image2 = this.document.querySelector('#image-2');
          if (image2 != null) elements.push(image2);
        }
  
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe(() => {});


    this.imageFitClass$ = this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      map(values => values.fitting),
      map(fit => {
        if (
          this.mangaReaderService.isWideImage(this.currentImage) &&
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
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }
  
  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (this.layoutMode !== LayoutMode.Double) return;
    if (this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit)) return;

    // If prev page was a spread, then we don't do + 1
    
    // if (this.mangaReaderService.isWideImage(this.currentImage2)) {
    //   this.currentImagePrev = this.getPage(this.pageNum);
    //   console.log('Setting Prev to ', this.pageNum);
    // } else {
    //   this.currentImagePrev = this.getPage(this.pageNum - 1); 
    //   console.log('Setting Prev to ', this.pageNum - 1);
    // }

    // TODO: Validate this statement: This needs to be capped at maxPages !this.isLastImage()
    this.currentImage = img[0];
    this.shouldRenderDouble$.pipe(take(1)).subscribe(shouldRenderDoublePage => {
      if (!shouldRenderDoublePage) return;
      console.log('Current canvas image page: ', this.readerService.imageUrlToPageNum(this.currentImage.src));
      console.log('Prev canvas image page: ', this.readerService.imageUrlToPageNum(this.currentImage2.src));

      this.currentImageNext = this.getPage(this.pageNum + 1);
      console.log('Setting Next to ', this.pageNum + 1);

      this.currentImagePrev = this.getPage(this.pageNum - 1);
      console.log('Setting Prev to ', this.pageNum - 1);
      

      console.log('Rendering Double Page');
      
      this.currentImage2 = this.currentImageNext;
      //  else {
      //   this.currentImage2 = this.currentImagePrev;
      // }
    });

    

    this.imageHeight.emit(this.currentImage.height);
    this.cdRef.markForCheck();
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(direction: PAGING_DIRECTION): number {
    if (this.layoutMode !== LayoutMode.Single || this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit)) return 0;
    // If prev page:
    switch (direction) {
      case PAGING_DIRECTION.FORWARD:
        return (
          !this.mangaReaderService.isCoverImage(this.pageNum) &&
          !this.mangaReaderService.isWideImage(this.currentImage) &&
          !this.mangaReaderService.isWideImage(this.currentImageNext) &&
          !this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages) &&
          !this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)
          ? 2 : 1);
      case PAGING_DIRECTION.BACKWARDS:
        return !(
          this.mangaReaderService.isCoverImage(this.pageNum)
          || this.mangaReaderService.isWideImage(this.currentImagePrev)
        ) ? 2 : 1;
    }
  }
  reset(): void {}

}
