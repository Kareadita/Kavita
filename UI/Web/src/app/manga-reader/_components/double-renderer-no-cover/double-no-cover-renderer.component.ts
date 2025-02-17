import { DOCUMENT, NgIf, NgClass, AsyncPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { Observable, of, map, tap, shareReplay, filter, combineLatest } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { ReaderService } from 'src/app/_services/reader.service';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { DEBUG_MODES } from '../../_models/renderer';
import { MangaReaderService } from '../../_service/manga-reader.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SafeStylePipe } from '../../../_pipes/safe-style.pipe';

/**
 * Renders 2 pages except on last page, and before a wide image
 */
@Component({
    selector: 'app-double-no-cover-renderer',
    templateUrl: './double-no-cover-renderer.component.html',
    styleUrls: ['./double-no-cover-renderer.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgIf, NgClass, AsyncPipe, SafeStylePipe]
})
export class DoubleNoCoverRendererComponent implements OnInit {

  @Input({required: true}) readerSettings$!: Observable<ReaderSetting>;
  @Input({required: true}) image$!: Observable<HTMLImageElement | null>;
  @Input({required: true}) bookmark$!: Observable<number>;
  @Input({required: true}) showClickOverlay$!: Observable<boolean>;
  @Input({required: true}) pageNum$!: Observable<{pageNum: number, maxPages: number}>;
  @Input({required: true}) getPage!: (pageNum: number) => HTMLImageElement;
  @Output() imageHeight: EventEmitter<number> = new EventEmitter<number>();
  private readonly destroyRef = inject(DestroyRef);

  debugMode: DEBUG_MODES = DEBUG_MODES.Logs;
  imageFitClass$!: Observable<string>;
  showClickOverlayClass$!: Observable<string>;
  readerModeClass$!: Observable<string>;
  layoutClass$!: Observable<string>;
  darkness$: Observable<string> = of('brightness(100%)');
  emulateBookClass$: Observable<string> = of('');
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
   * Determines if we should render a double page.
   * The general gist is if we are on double layout mode, the current page (first page) is not a cover image or a wide image
   * and the next page is not a wide image (as only non-wides should be shown next to each other).
   * @remarks This will always fail if the window's width is greater than the height
  */
  shouldRenderDouble$!: Observable<boolean>;


  get ReaderMode() {return ReaderMode;}
  get FITTING_OPTION() {return FITTING_OPTION;}
  get LayoutMode() {return LayoutMode;}



  constructor(private readonly cdRef: ChangeDetectorRef, public mangaReaderService: MangaReaderService,
    @Inject(DOCUMENT) private document: Document, public readerService: ReaderService) { }

  ngOnInit(): void {
    this.readerModeClass$ = this.readerSettings$.pipe(
      map(values => values.readerMode),
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.darkness$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.emulateBookClass$ = this.readerSettings$.pipe(
      map(data => data.emulateBook),
      map(enabled => enabled ? 'book-shadow' : ''),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.pageNum$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(pageInfo => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;

        this.currentImage = this.getPage(this.pageNum);
        this.currentImage2 = this.getPage(this.pageNum + 1);

        this.cdRef.markForCheck();
      }),
      filter(_ => this.isValid()),
    ).subscribe(() => {});

    this.shouldRenderDouble$ = this.pageNum$.pipe(
      takeUntilDestroyed(this.destroyRef),
      map((_) => this.shouldRenderDouble()),
      filter(_ => this.isValid()),
    );

    this.imageFitClass$ = this.readerSettings$.pipe(
      takeUntilDestroyed(this.destroyRef),
      map(values => values.fitting),
      filter(_ => this.isValid()),
      shareReplay()
    );

    this.layoutClass$ = combineLatest([this.shouldRenderDouble$, this.readerSettings$]).pipe(
      takeUntilDestroyed(this.destroyRef),
      map((value) =>  {
        if (value[0] && value[1].fitting === FITTING_OPTION.WIDTH) return 'fit-to-width-double-offset';
        if (value[0] && value[1].fitting === FITTING_OPTION.HEIGHT) return 'fit-to-height-double-offset';
        if (value[0] && value[1].fitting === FITTING_OPTION.ORIGINAL) return 'original-double-offset';
        return '';
      }),
      filter(_ => this.isValid()),
    );


    this.readerSettings$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(values => {
        this.layoutMode = values.layoutMode;
        this.pageSplit = values.pageSplit;
        this.cdRef.markForCheck();
      })
    ).subscribe(() => {});

    this.bookmark$.pipe(
      takeUntilDestroyed(this.destroyRef),
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
  }

  shouldRenderDouble() {
    if (!this.isValid()) return false;

    // if (this.mangaReaderService.isCoverImage(this.pageNum)) {
    //   this.debugLog('Not rendering double as current page is cover image');
    //   return false;
    // }

    if (this.mangaReaderService.isWidePage(this.pageNum) ) {
      this.debugLog('Not rendering double as current page is wide image');
      return false;
    }

    if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
      this.debugLog('Not rendering double as current page is last');
      return false;
    }

    if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
      this.debugLog('Not rendering double as current page is last');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum + 1) ) {
      this.debugLog('Not rendering double as next page is wide image');
      return false;
    }

    return true;
  }

  isValid() {
    return this.layoutMode === LayoutMode.DoubleNoCover;
  }

  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;

    // First load, switching from double manga -> double, this is 0 and thus not rendering
    if (!this.shouldRenderDouble() && (this.currentImage.height || img[0].height) > 0) {
      this.imageHeight.emit(this.currentImage.height || img[0].height);
      return;
    }

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
    if (!this.isValid()) return 0;

    switch (direction) {
      case PAGING_DIRECTION.FORWARD:
        if (this.mangaReaderService.isWidePage(this.pageNum)) {
          this.debugLog('Moving forward 1 page as current page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWidePage(this.pageNum + 1)) {
          this.debugLog('Moving forward 1 page as next page is wide');
          return 1;
        }
        if (this.mangaReaderService.isCoverImage(this.pageNum)) {
          this.debugLog('Moving forward 2 page as on cover image');
          return 2;
        }
        if (this.mangaReaderService.isSecondLastImage(this.pageNum, this.maxPages)) {
          this.debugLog('Moving forward 1 page as 2 pages left');
          return 1;
        }
        if (this.mangaReaderService.isLastImage(this.pageNum, this.maxPages)) {
          this.debugLog('Moving forward 1 page as 1 page left');
          return 1;
        }
        this.debugLog('Moving forward 2 pages');
        return 2;
      case PAGING_DIRECTION.BACKWARDS:

      if (this.mangaReaderService.isCoverImage(this.pageNum - 1)) {
        // TODO: If we are moving back and prev page is cover and we are not showing on right side, then move back twice as if we did once, we would show pageNum twice
        this.debugLog('Moving back 1 page as on cover image');
        return 2;
      }

        if (this.mangaReaderService.isCoverImage(this.pageNum)) {
          this.debugLog('Moving back 1 page as on cover image');
          return 2;
        }

        if (this.mangaReaderService.adjustForDoubleReader(this.pageNum - 1) != this.pageNum - 1 && !this.mangaReaderService.isWidePage(this.pageNum - 2)) {
          this.debugLog('Moving back 2 pages as previous pair should be in a pair');
          return 2;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum)) {
          this.debugLog('Moving back 1 page as current page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(this.pageNum - 1)) {
          this.debugLog('Moving back 1 page as prev page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWidePage(this.pageNum - 2)) {
          this.debugLog('Moving back 1 page as 2 pages back is wide');
          return 1;
        }

        this.debugLog('Moving back 2 pages');
        return 2;
    }
  }
  reset(): void {}

  getBookmarkPageCount(): number {
    return this.shouldRenderDouble() ? 2 : 1;
  }

  debugLog(message: string, extraData?: any) {
    if (!(this.debugMode & DEBUG_MODES.Logs)) return;

    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }
}
