import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  model,
  OnDestroy,
  OnInit,
  Renderer2,
  RendererStyleFlags2,
  resource,
  Signal,
  ViewChild,
  ViewContainerRef
} from '@angular/core';
import {DOCUMENT, NgClass, NgStyle, NgTemplateOutlet, PercentPipe} from '@angular/common';
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {firstValueFrom, forkJoin, fromEvent, merge, of, switchMap} from 'rxjs';
import {catchError, debounceTime, distinctUntilChanged, filter, take, tap} from 'rxjs/operators';
import {Chapter} from 'src/app/_models/chapter';
import {NavService} from 'src/app/_services/nav.service';
import {CHAPTER_ID_DOESNT_EXIST, CHAPTER_ID_NOT_FETCHED, ReaderService} from 'src/app/_services/reader.service';
import {SeriesService} from 'src/app/_services/series.service';
import {DomSanitizer, SafeHtml, Title} from '@angular/platform-browser';
import {BookService} from '../../_services/book.service';
import {Breakpoint, KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {BookChapterItem} from '../../_models/book-chapter-item';
import {animate, state, style, transition, trigger} from '@angular/animations';
import {Stack} from 'src/app/shared/data-structures/stack';
import {ReadingDirection} from 'src/app/_models/preferences/reading-direction';
import {WritingStyle} from "../../../_models/preferences/writing-style";
import {MangaFormat} from 'src/app/_models/manga-format';
import {LibraryService} from 'src/app/_services/library.service';
import {LibraryType} from 'src/app/_models/library/library';
import {BookTheme} from 'src/app/_models/preferences/book-theme';
import {BookPageLayoutMode} from 'src/app/_models/readers/book-page-layout-mode';
import {PageStyle} from '../reader-settings/reader-settings.component';
import {ThemeService} from 'src/app/_services/theme.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {PAGING_DIRECTION} from 'src/app/manga-reader/_models/reader-enums';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {BookLineOverlayComponent} from "../book-line-overlay/book-line-overlay.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfile} from "../../../_models/preferences/reading-profiles";
import {ConfirmService} from "../../../shared/confirm.service";
import {EpubReaderMenuService} from "../../../_services/epub-reader-menu.service";
import {EpubReaderSettingsService, ReaderSettingUpdate} from "../../../_services/epub-reader-settings.service";
import {ColumnLayoutClassPipe} from "../../_pipes/column-layout-class.pipe";
import {WritingStyleClassPipe} from "../../_pipes/writing-style-class.pipe";
import {ReadTimeLeftPipe} from "../../../_pipes/read-time-left.pipe";
import {PageBookmark} from "../../../_models/readers/page-bookmark";
import {EpubHighlightService} from "../../../_services/epub-highlight.service";
import {AnnotationService} from "../../../_services/annotation.service";
import {Annotation} from "../../_models/annotations/annotation";
import {NgxSliderModule} from "@angular-slider/ngx-slider";
import {ProgressBookmark} from "../../../_models/readers/progress-bookmark";
import {LayoutMeasurementService} from "../../../_services/layout-measurement.service";
import {ColorscapeService} from "../../../_services/colorscape.service";
import {environment} from "../../../../environments/environment";
import {LoadPageEvent} from "../_drawers/view-bookmarks-drawer/view-bookmark-drawer.component";
import {FontService} from "../../../_services/font.service";
import afterFrame from "afterframe";


interface HistoryPoint {
  /**
   * Page Number
   */
  page: number;
  /**
   * XPath to scroll to
   */
  scrollPart: string;
}

type Container = {left: number, right: number, top: number, bottom: number, width: number, height: number};

const TOP_OFFSET = -(50 + 10) * 1.5; // px the sticky header takes up // TODO: Do I need this or can I change it with new fixed top height

const COLUMN_GAP = 20; // px
/**
 * Styles that should be applied on the top level book-content tag
 */
const pageLevelStyles = ['margin-left', 'margin-right', 'font-size'];
/**
 * Styles that should be applied on every element within book-content tag
 */
const elementLevelStyles = ['line-height', 'font-family'];

/**
 * Minimum size to be assigned a bookmark
 */
const minImageSize = {
  height: 200,
  width: 100
};

/**
 * A slight delay before scrolling, to ensure everything has rendered correctly
 * Ex. after jumping in the ToC
 */
const SCROLL_DELAY = 10;

@Component({
    selector: 'app-book-reader',
    templateUrl: './book-reader.component.html',
    styleUrls: ['./book-reader.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    animations: [
        trigger('isLoading', [
            state('false', style({ opacity: 1 })),
            state('true', style({ opacity: 0 })),
            transition('false <=> true', animate('200ms'))
        ]),
        trigger('fade', [
            state('true', style({ opacity: 0 })),
            state('false', style({ opacity: 0.5 })),
            transition('false <=> true', animate('4000ms'))
        ])
    ],
  imports: [NgTemplateOutlet, NgStyle, NgClass, NgbTooltip,
    BookLineOverlayComponent, TranslocoDirective, ColumnLayoutClassPipe, WritingStyleClassPipe, ReadTimeLeftPipe, PercentPipe, NgxSliderModule],
  providers: [EpubReaderSettingsService, LayoutMeasurementService],
})
export class BookReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly readerService = inject(ReaderService);
  private readonly epubHighlightService = inject(EpubHighlightService);
  private readonly renderer = inject(Renderer2);
  private readonly navService = inject(NavService);
  private readonly toastr = inject(ToastrService);
  private readonly domSanitizer = inject(DomSanitizer);
  private readonly bookService = inject(BookService);
  private readonly scrollService = inject(ScrollService);
  protected readonly utilityService = inject(UtilityService);
  private readonly libraryService = inject(LibraryService);
  private readonly themeService = inject(ThemeService);
  private readonly confirmService = inject(ConfirmService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly epubMenuService = inject(EpubReaderMenuService);
  protected readonly readerSettingsService = inject(EpubReaderSettingsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly annotationService = inject(AnnotationService);
  private readonly titleService = inject(Title);
  private readonly document = inject(DOCUMENT);
  private readonly layoutService = inject(LayoutMeasurementService);
  private readonly colorscapeService = inject(ColorscapeService);
  private readonly fontService = inject(FontService);

  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;
  readingProfile!: ReadingProfile;

  /**
   * Reading List id. Defaults to -1.
   */
  readingListId: number = CHAPTER_ID_DOESNT_EXIST;

   /**
    * If this is true, no progress will be saved.
    */
  incognitoMode = model<boolean>(false);

   /**
    * If this is true, chapters will be fetched in the order of a reading list,
    * rather than natural series order.
    */
  readingListMode: boolean = false;

  /**
   * The actual pages from the epub, used for showing on table of contents.
   * This must be here as we need access to it for scroll anchors
   */
  chapters: Array<BookChapterItem> = [];
  /**
   * Current Page
   */
  pageNum = model<number>(0);
  /**
   * Max Pages
   */
  maxPages = model<number>(1);
  /**
   * This allows for exploration into different chapters
   */
  adhocPageHistory: Stack<HistoryPoint> = new Stack<HistoryPoint>();
  /**
   * A stack of the chapter ids we come across during continuous reading mode. When we traverse a boundary, we use this to avoid extra API calls.
   * @see Stack
   * TODO: See if continuousChaptersStack can be moved into reader service so we can reduce code duplication between readers (and also use ChapterInfo with it instead)
   */
  continuousChaptersStack: Stack<number> = new Stack();
  /*
   * The current page only contains an image. This is used to determine if we should show the image in the center of the screen.
   */
  isSingleImagePage = false;
  /**
   * If the word/line overlay is open
   */
  isLineOverlayOpen = model<boolean>(false);
  /**
   * If the action bar (menu bars) is visible
   */
  actionBarVisible = model<boolean>(true);
  /**
   * If we are loading from backend
   */
  isLoading = model<boolean>(true);
  /**
   * Title of the book. Rendered in action bar
   */
  bookTitle = model<string>('');
  /**
   * Authors of the book. Rendered in action bar
   */
  authorText = model<string>('');
  /**
   * The boolean that decides if the clickToPaginate overlay is visible or not.
   */
  clickToPaginateVisualOverlay = false;
  clickToPaginateVisualOverlayTimeout: any = undefined; // For animation
  clickToPaginateVisualOverlayTimeout2: any = undefined; // For kicking off animation, giving enough time to render html
  updateImageSizeTimeout: any = undefined;
  /**
   * This is the html we get from the server
   */
  page = model<SafeHtml | undefined>(undefined);
  /**
   * Next Chapter Id. This is not guaranteed to be a valid ChapterId. Prefetched on page load (non-blocking).
   */
   nextChapterId: number = CHAPTER_ID_NOT_FETCHED;
   /**
    * Previous Chapter Id. This is not guaranteed to be a valid ChapterId. Prefetched on page load (non-blocking).
    */
   prevChapterId: number = CHAPTER_ID_NOT_FETCHED;
   /**
    * Is there a next chapter. If not, this will disable UI controls.
    */
   nextChapterDisabled: boolean = false;
   /**
    * Is there a previous chapter. If not, this will disable UI controls.
    */
   prevChapterDisabled: boolean = false;
   /**
    * Has the next chapter been prefetched. Prefetched means the backend will cache the files.
    */
   nextChapterPrefetched: boolean = false;
   /**
    * Has the previous chapter been prefetched. Prefetched means the backend will cache the files.
    */
   prevChapterPrefetched: boolean = false;
  /**
   * If the prev page allows a page change to occur.
   */
   prevPageDisabled = false;
   /**
    * If the next page allows a page change to occur.
    */
   nextPageDisabled = false;

  /**
   * Offset for drawer and rendering canvas. Fixed to 62px.
   */
  topOffset: number = 38;
  /**
   * Used for showing/hiding bottom action bar. Calculates if there is enough scroll to show it.
   * Will hide if all content in book is absolute positioned
   */
  horizontalScrollbarNeeded = false;
  scrollbarNeeded = model<boolean>(false);

  /**
   * Used solely for fullscreen to apply a hack
   */
  darkMode = model<boolean>(true);
  readingTimeLeftResource =  resource({
    params: () => ({
      chapterId: this.chapterId,
      seriesId: this.seriesId,
      pageNumber: this.pageNum(),
    }),
    loader: async ({params}) => {
      return firstValueFrom(this.readerService.getTimeLeftForChapter(params.seriesId, params.chapterId));
    }
  });

  imageBookmarks = model<PageBookmark[]>([]);
  annotationToLoad = model<number>(-1);

  /**
   * Anchors that map to the page number. When you click on one of these, we will load a given page up for the user.
   */
  pageAnchors: {[n: string]: number } = {};
  currentPageAnchor: string = '';
  /**
   * Last seen progress part path. This is not descoped.
   */
  lastSeenScrollPartPath: string = '';
  /**
   * Library Type used for rendering chapter or issue
   */
   libraryType: LibraryType = LibraryType.Book;
  /**
   * If the web browser is in fullscreen mode
   */
  isFullscreen: boolean = false;


  /**
   * Width of the document (in non-column layout), used for column layout virtual paging
   */
  windowWidth = model<number>(0);
  windowHeight = model<number>(0);

  /**
   * used to track if a click is a drag or not, for opening menu
   */
  mousePosition = {
    x: 0,
    y: 0
  };

  /**
   * Used to keep track of direction user is paging, to help with virtual paging on column layout
   */
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  /**
   * When the user is highlighting something, then we remove pagination
   */
  hidePagination = model<boolean>(false);

  /**
   * Used to refresh the Personal PoC
   */
  refreshPToC: EventEmitter<void> = new EventEmitter<void>();
  /**
   * Will be set to false once the initial page is injected, signalling that annotations can now process changes
   */
  firstLoad: boolean = true;

  /**
   * Injects information to help debug issues
   */
  debugMode = model<boolean>(!environment.production && true);

  /**
   * Will be set to true if this.scroll(...) is called but the actual scroll is still delayed
   * This can also be used to debug glitches or race conditions related to page scrolling
   * For instance, when we invoke a scroll action, but another scroll is scheduled to be triggered afterward
   */
  hasDelayedScroll: boolean = false;


  @ViewChild('bookContainer', {static: false}) bookContainerElemRef!: ElementRef<HTMLDivElement>;
  /**
   * book-content class
   */
  @ViewChild('readingHtml', {static: false}) bookContentElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('readingHtml', { read: ViewContainerRef }) readingContainer!: ViewContainerRef;

  @ViewChild('readingSection', {static: false}) readingSectionElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('stickyTop', {static: false}) stickyTopElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('reader', {static: false}) reader!: ElementRef;



  protected readonly layoutMode = this.readerSettingsService.layoutMode;
  protected readonly pageStyles = this.readerSettingsService.pageStyles;
  protected readonly immersiveMode = this.readerSettingsService.immersiveMode;
  protected readonly readingDirection = this.readerSettingsService.readingDirection;
  protected readonly writingStyle = this.readerSettingsService.writingStyle;
  protected readonly clickToPaginate = this.readerSettingsService.clickToPaginate;

  protected columnWidth!: Signal<string>;
  protected columnHeight!: Signal<string>;
  protected verticalBookContentWidth!: Signal<string>;
  protected virtualizedPageNum!: Signal<number>;
  protected virtualizedMaxPages!: Signal<number>;
  protected bookContentPaddingBottom = computed(() => {
    const layoutMode = this.layoutMode();
    if (layoutMode !== BookPageLayoutMode.Default) return '0px';
    return '40px';
  });

  pageWidthForPagination = computed(() => {
    const layoutMode = this.layoutMode();
    const writingStyle = this.writingStyle();

    if (layoutMode === BookPageLayoutMode.Default && writingStyle === WritingStyle.Vertical && this.horizontalScrollbarNeeded) {
      return 'unset';
    }
    return '100%'
  });

  /**
   * Disables the Left most button
   */
  isPrevDisabled = computed(() => {
    const readingDirection = this.readingDirection();

    if (readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Previous button
      return this.isPrevPageDisabled();
    }

    // Acting as a Next button
    return this.isNextPageDisabled();
  });

  isNextDisabled = computed(() => {
    const readingDirection = this.readingDirection();

    if (readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Next button
      return this.isNextPageDisabled();
    }
    // Acting as Previous button
    return this.isPrevPageDisabled();
  });

  shouldShowMenu = computed(() => {
    const immersiveMode = this.immersiveMode();
    const isDrawerOpen = this.epubMenuService.isDrawerOpen();
    const actionBarVisible = this.actionBarVisible();

    return actionBarVisible || !immersiveMode || isDrawerOpen;
  });

  shouldShowBottomActionBar = computed(() => {
    const layoutMode = this.layoutMode();
    const scrollbarNeeded = this.scrollbarNeeded();
    const writingStyle = this.writingStyle();
    const immersiveMode = this.immersiveMode();
    const actionBarVisible = this.actionBarVisible();
    const isDrawerOpen = this.epubMenuService.isDrawerOpen();

    const isColumnMode = layoutMode !== BookPageLayoutMode.Default;
    const isVerticalLayout = writingStyle === WritingStyle.Vertical;


    const baseCondition = (scrollbarNeeded || isColumnMode)
      && !(isVerticalLayout && !isColumnMode);

    const showForVerticalDefault = !isColumnMode && isVerticalLayout;

    const showWhenDefaultLayout = !isColumnMode && !isVerticalLayout;

    const otherCondition = !immersiveMode || isDrawerOpen || actionBarVisible;

    return (baseCondition || showForVerticalDefault || showWhenDefaultLayout) && otherCondition;
  });


  isNextPageDisabled() {
    const condition = (this.nextPageDisabled || this.nextChapterId === CHAPTER_ID_DOESNT_EXIST) && this.pageNum() + 1 > this.maxPages() - 1;

    if (this.layoutMode() !== BookPageLayoutMode.Default) {
      const [currentVirtualPage, totalVirtualPages, _] = this.getVirtualPage();
      return condition && currentVirtualPage === totalVirtualPages;
    }

    return condition;
  }

  isPrevPageDisabled() {
    const condition = (this.prevPageDisabled || this.prevChapterId === CHAPTER_ID_DOESNT_EXIST) && this.pageNum() === 0;

    if (this.layoutMode() !== BookPageLayoutMode.Default) {
      const [currentVirtualPage,, ] = this.getVirtualPage();
      return condition && currentVirtualPage === 1;
    }

    return condition;
  }

  /**
   * Determines if we show >> or >
   */
  get IsNextChapter(): boolean {
    if (this.layoutMode() === BookPageLayoutMode.Default) {
      return this.pageNum() + 1 >= this.maxPages();
    }

    const [currentVirtualPage, totalVirtualPages, _] = this.getVirtualPage();
    if (this.bookContentElemRef == null) return this.pageNum() + 1 >= this.maxPages();

    return this.pageNum() + 1 >= this.maxPages() && (currentVirtualPage === totalVirtualPages);
  }
  /**
   * Determines if we show << or <
   */
  get IsPrevChapter(): boolean {
    if (this.layoutMode() === BookPageLayoutMode.Default) {
      return this.pageNum() === 0;
    }

    const [currentVirtualPage,,] = this.getVirtualPage();
    if (this.bookContentElemRef == null) return this.pageNum() + 1 >= this.maxPages();

    return this.pageNum() === 0 && (currentVirtualPage === 0);
  }


  pageHeightForPagination = computed(() => {
    const layoutMode = this.layoutMode();
    const immersiveMode = this.immersiveMode();

    if (layoutMode === BookPageLayoutMode.Default) {
      // Ensure Angular updates this pageHeightForPagination when these signal have an update
      if (this.isLoading()) return;
      this.windowHeight();
      this.writingStyle();

      // if the book content is less than the height of the container, override and return height of container for pagination area
      if (this.bookContainerElemRef?.nativeElement?.clientHeight > this.bookContentElemRef?.nativeElement?.clientHeight) {
        return (this.bookContainerElemRef?.nativeElement?.clientHeight || 0) + 'px';
      }

      return (this.bookContentElemRef?.nativeElement?.scrollHeight || 0) - ((this.topOffset * (immersiveMode ? 0 : 1)) * 2) + 'px';
    }

    return '100%';
  });

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
    this.themeService.clearThemes();
    this.cdRef.markForCheck();

    this.columnWidth = computed(() => {
      const layoutMode = this.layoutMode();
      const writingStyle = this.writingStyle();

      // const windowWidth = this.windowWidth();
      // const marginLeft = this.pageStyles()['margin-left'];
      // const margin = (this.convertVwToPx(parseInt(marginLeft, 10)) * 2);
      const base = writingStyle === WritingStyle.Vertical ? this.pageHeight() : this.pageWidth();


      switch (layoutMode) {
        case BookPageLayoutMode.Default:
          return 'unset';
        case BookPageLayoutMode.Column1:
          return Math.round(base / 2) + 'px';
        case BookPageLayoutMode.Column2:
          return Math.round(base / 4) + 'px'
        default:
          return 'unset';
      }
    });

    this.columnHeight = computed(() => {
      // Note: Computed signals need to be called before if statement to ensure it's called when a dep signal is updated
      const layoutMode = this.layoutMode();
      const writingStyle = this.writingStyle();
      const windowHeight = this.windowHeight();


      if (layoutMode !== BookPageLayoutMode.Default || writingStyle === WritingStyle.Vertical) {
        // Take the height after page loads, subtract the top/bottom bar
        const height = windowHeight - (this.topOffset * 2);
        return height + 'px';
      }
      return 'unset';
    });

    this.verticalBookContentWidth = computed(() => {
      const layoutMode = this.layoutMode();
      const writingStyle = this.writingStyle();
      const verticalPageWidth = this.getVerticalPageWidth();
      const pageStyles = this.pageStyles() ?? this.readerSettingsService.getDefaultPageStyles(); // Needed in inner method (not sure if Signals handle)


      if (layoutMode !== BookPageLayoutMode.Default && writingStyle !== WritingStyle.Horizontal) {
        return `${verticalPageWidth}px`;
      }
      return '';
    });

    this.virtualizedPageNum = computed(() => {
      return this.pageNum();
    });

    this.virtualizedMaxPages = computed(() => {
      return this.maxPages();
    });

    effect(() => {
      const annotationEvent = this.annotationService.events();
      const pageNum = this.pageNum();

      if (annotationEvent == null || annotationEvent.pageNumber !== pageNum) return;
      if (this.firstLoad) return;

      if (annotationEvent.type === 'edit') return; // Let signalR propagate state (or component can)

      this.firstLoad = true;
      const scrollProgress = this.reader.nativeElement?.scrollTop || this.scrollService.scrollPosition;

      if (scrollProgress > 0) {
        this.loadPage(undefined, scrollProgress); // This will force loading exactly on the scroll
      } else {
        this.loadPage(this.lastSeenScrollPartPath);
      }
    });


    // Prefetch next/prev chapter data based on page number
    effect(() => {
      const pageNum = this.pageNum();
      const maxPages = this.maxPages();

      if (pageNum >= maxPages - 10) {
        // Tell server to cache the next chapter
        if (!this.nextChapterPrefetched && this.nextChapterId !== CHAPTER_ID_DOESNT_EXIST) {
          this.readerService.getChapterInfo(this.nextChapterId).pipe(catchError(err => {
            this.nextChapterDisabled = true;
            console.error(err);
            return of(null);
          })).subscribe(res => {
            this.nextChapterPrefetched = true;
          });
        }
      }

      if (pageNum <= 10) {
        if (!this.prevChapterPrefetched && this.prevChapterId !== CHAPTER_ID_DOESNT_EXIST) {
          this.readerService.getChapterInfo(this.prevChapterId).pipe(catchError(err => {
            this.prevChapterDisabled = true;
            console.error(err);
            return of(null);
          })).subscribe(res => {
            this.prevChapterPrefetched = true;
          });
        }
      }
    });
  }

  /**
   * After the page has loaded, set up the scroll handler. The scroll handler has 2 parts. One is if there are page anchors setup (aka page anchor elements linked with the
   * table of content) then we calculate what has already been reached and grab the last reached one to save progress. If page anchors aren't setup (toc missing), then try to save progress
   * based on the last seen scroll part (xpath).
   */
  ngAfterViewInit() {

    // Hook up the observers
    this.setupObservers();


    // check scroll offset and if offset is after any of the "id" markers, save progress
    fromEvent(this.reader.nativeElement, 'scroll')
      .pipe(
        debounceTime(200),
        filter(_ => !this.isLoading()),
        tap(_ => this.handleScrollEvent()),
        takeUntilDestroyed(this.destroyRef))
      .subscribe();

    const mouseMove$ = fromEvent<MouseEvent>(this.bookContainerElemRef.nativeElement, 'mousemove');
    const touchMove$ = fromEvent<TouchEvent>(this.bookContainerElemRef.nativeElement, 'touchmove');

    merge(mouseMove$, touchMove$)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        tap((e) => {
          const selection = window.getSelection();
          this.hidePagination.set(selection !== null && selection.toString().trim() !== '');
          this.cdRef.markForCheck();
        })
      )
      .subscribe();

    const mouseUp$ = fromEvent<MouseEvent>(this.bookContainerElemRef.nativeElement, 'mouseup');
    const touchEnd$ = fromEvent<TouchEvent>(this.bookContainerElemRef.nativeElement, 'touchend');

    merge(mouseUp$, touchEnd$)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        tap(_ => this.hidePagination.set(false))
      ).subscribe();
  }

  private setupObservers() {
    this.layoutService.observeElement(
      this.bookContentElemRef.nativeElement,
      'bookContent'
    );

    this.layoutService.observeElement(
      this.readingSectionElemRef.nativeElement,
      'readingSection'
    );
  }

  /**
   * Updates the TOC current page anchor, last scene path and saves progress
   */
  handleScrollEvent() {

    // TODO: See if we can move this to a service for ToC
    // Highlight the current chapter we are on
    if (Object.keys(this.pageAnchors).length !== 0) {
      // get the height of the document, so we can capture markers that are halfway on the document viewport
      const verticalOffset = this.reader.nativeElement?.scrollTop || (this.scrollService.scrollPosition + (this.document.body.offsetHeight / 2));

      const alreadyReached = Object.values(this.pageAnchors).filter((i: number) => i <= verticalOffset);
      if (alreadyReached.length > 0) {
        this.currentPageAnchor = Object.keys(this.pageAnchors)[alreadyReached.length - 1];
      } else {
        this.currentPageAnchor = '';
      }

      this.cdRef.markForCheck();
    }

    // Find the element that is on screen to bookmark against
    const xpath: string | null | undefined = this.getFirstVisibleElementXPath();
    if (xpath !== null && xpath !== undefined) {
      this.lastSeenScrollPartPath = xpath; // Keep this scoped so we can appropriately handle before saving
    }

    if (this.lastSeenScrollPartPath !== '') {
      this.saveProgress();

      if (this.debugMode()) {
        this.logSelectedElement();
      }

    }
  }

  saveProgress() {
    if (!this.incognitoMode()) {
      let tempPageNum = this.pageNum();
      if (this.pageNum() == this.maxPages() - 1) {
        tempPageNum = this.pageNum() + 1;
      }

      const descopedPath = this.readerService.descopeBookReaderXpath(this.lastSeenScrollPartPath);
      this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, this.chapterId, tempPageNum, descopedPath).subscribe();
    }

  }

  ngOnDestroy(): void {
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout);
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);

    this.readerService.disableWakeLock();

    // Remove any debug viewport things
    this.document.querySelector('#test')?.remove();

    this.themeService.clearBookTheme();

    this.themeService.currentTheme$.pipe(take(1)).subscribe(theme => {
      this.themeService.setTheme(theme.name);
    });

    this.navService.showNavBar();
    this.navService.showSideNav();
  }

  async ngOnInit() {
    this.fontService.getFonts().subscribe(fonts => {
      fonts.filter(f => f.name !== FontService.DefaultEpubFont).forEach(font => {
        this.fontService.getFontFace(font).load().then(loadedFace => {
          (this.document as any).fonts.add(loadedFace);
        });
      });
    });

    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');

    if (libraryId === null || seriesId === null || chapterId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);
    this.incognitoMode.set(this.route.snapshot.queryParamMap.get('incognitoMode') === 'true');

    // If an annotation exists, load it and
    if (this.route.snapshot.queryParamMap.has('annotation')) {
      const annotationId = parseInt(this.route.snapshot.queryParamMap.get('annotation') ?? '0', 10);
      this.annotationToLoad.set(annotationId);

      // Remove the annotation from the url
      const queryParams = { ...this.route.snapshot.queryParams };
      delete queryParams['annotation'];

      // Navigate to same route with updated query params
      await this.router.navigate([], {
        relativeTo: this.route,
        queryParams,
        replaceUrl: true // This prevents adding to browser history
      });
    }

    const readingListId = this.route.snapshot.queryParamMap.get('readingListId');
    if (readingListId != null) {
      this.readingListMode = true;
      this.readingListId = parseInt(readingListId, 10);
    }
    this.cdRef.markForCheck();

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(async (data) => {
      this.readingProfile = data['readingProfile'];
      this.cdRef.markForCheck();

      if (this.readingProfile == null) {
        this.router.navigateByUrl('/home');
        return;
      }

      await this.init();
    });


    const resize$ = fromEvent(window, 'resize');
    const orientationChange$ = fromEvent(window, 'orientationchange');

    merge(resize$, orientationChange$)
      .pipe(
        debounceTime(200),
        takeUntilDestroyed(this.destroyRef),
        tap(_ => this.onResize())
      )
      .subscribe();
  }

  async init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.cdRef.markForCheck();

    this.loadImageBookmarks();


    this.bookService.getBookInfo(this.chapterId, true).subscribe(async (info) => {
      if (this.readingListMode && info.seriesFormat !== MangaFormat.EPUB) {
        // Redirect to the manga reader.
        const params = this.readerService.getQueryParamsObject(this.incognitoMode(), this.readingListMode, this.readingListId);
        await this.router.navigate(this.readerService.getNavigationArray(info.libraryId, info.seriesId, this.chapterId, info.seriesFormat), {queryParams: params});
        return;
      }

      this.bookTitle.set(info.bookTitle);
      this.titleService.setTitle('Kavita - ' + this.bookTitle());
      this.cdRef.markForCheck();

      await this.readerSettingsService.initialize(this.seriesId, this.readingProfile);

      // Ensure any changes in the reader settings are applied to the reader
      this.readerSettingsService.settingUpdates$.pipe(
        takeUntilDestroyed(this.destroyRef),
        tap((update) => this.handleReaderSettingsUpdate(update))
      ).subscribe();

      forkJoin({
        chapter: this.seriesService.getChapter(this.chapterId),
        progress: this.readerService.getProgress(this.chapterId),
        chapters: this.bookService.getBookChapters(this.chapterId),
      }).subscribe({
        next: ({chapter, progress, chapters}) => {
          this.authorText.set(chapter.writers.map(p => p.name).join(', '));
          this.setupBookReader(chapter, progress, chapters);
        },
        error: () => {
          setTimeout(() => {
            this.closeReader();
          }, 200);
        }
      });
    });
  }

  private setupBookReader(chapter: Chapter, progress: ProgressBookmark, chapters: BookChapterItem[]) {
    this.chapter = chapter;
    this.volumeId = chapter.volumeId;
    this.chapters = chapters;
    this.maxPages.set(chapter.pages);
    //this.pageNum.set(progress.pageNum);
    this.setPageNum(progress.pageNum);
    this.cdRef.markForCheck();

    if (progress.bookScrollId) {
      // Don't descope here as document hasn't loaded
      this.lastSeenScrollPartPath = progress.bookScrollId;
    }

    this.continuousChaptersStack.push(this.chapterId);

    this.libraryService.getLibraryType(this.libraryId).subscribe(type => {
      this.libraryType = type;
    });

    if (this.pageNum() >= this.maxPages()) {
      this.pageNum.set(this.maxPages() - 1);
      this.saveProgress();
    }

    this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).subscribe(chapterId => {
      this.nextChapterId = chapterId;
      if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
        this.nextChapterDisabled = true;
        this.nextChapterPrefetched = true;
        return;
      }
    });
    this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).subscribe(chapterId => {
      this.prevChapterId = chapterId;
      if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
        this.prevChapterDisabled = true;
        this.prevChapterPrefetched = true; // If there is no prev chapter, then mark it as prefetched
        return;
      }
    });

    // If there is an annotation to load, prioritize it
    if (this.annotationToLoad() > 0) {
      this.annotationService.getAnnotation(this.annotationToLoad()).subscribe((data) => {
        this.annotationToLoad.set(-1);
        this.setPageNum(data.pageNumber);
        this.loadPage(data.xPath || undefined);
        this.readerService.enableWakeLock(this.reader.nativeElement);
      });
    } else {
      // Check if user progress has part, if so load it so we scroll to it
      this.loadPage(progress.bookScrollId || undefined);
      this.readerService.enableWakeLock(this.reader.nativeElement);
    }
  }

  onResize(){
    // Update the window Height
    this.updateWidthAndHeightCalcs();
    this.updateImageSizes();

    // Refresh page styles to handle margin changes on window resize
    this.applyPageStyles(this.pageStyles());

    // Attempt to restore the reading position
    this.snapScrollOnResize();

    afterFrame(() => {
      this.injectImageBookmarkIndicators(true);
    });
  }

  /**
   * Only applies to non BookPageLayoutMode.Default and WritingStyle Horizontal
   * @private
   */
  private snapScrollOnResize() {
    const layoutMode = this.layoutMode();
    if (layoutMode === BookPageLayoutMode.Default) return;

    const resumeElement = this.lastSeenScrollPartPath || (this.getFirstVisibleElementXPath() ?? '');
    if (resumeElement) {

      if (this.debugMode()) {
        const element = this.getElementFromXPath(resumeElement);
        //console.log('Attempting to snap to element: ', element);
        this.logSelectedElement('yellow');
      }

      this.scrollTo(resumeElement, 30); // This works pretty well, but not perfect
    }
  }

  @HostListener('window:keydown', ['$event'])
  async handleKeyPress(event: KeyboardEvent) {
    const activeElement = document.activeElement as HTMLElement;
    const isInputFocused = activeElement.tagName === 'INPUT'
      || activeElement.tagName === 'TEXTAREA' ||
      activeElement.contentEditable === 'true' ||
      activeElement.closest('.ql-editor'); // Quill editor class

    if (isInputFocused) return;

    switch (event.key) {
      case KEY_CODES.RIGHT_ARROW:
        this.movePage(this.readingDirection() === ReadingDirection.LeftToRight ? PAGING_DIRECTION.FORWARD : PAGING_DIRECTION.BACKWARDS);
        break;
      case KEY_CODES.LEFT_ARROW:
        this.movePage(this.readingDirection() === ReadingDirection.LeftToRight ? PAGING_DIRECTION.BACKWARDS : PAGING_DIRECTION.FORWARD);
        break;
      case KEY_CODES.ESC_KEY:
        const isHighlighting = window.getSelection()?.toString() != '';
        if (isHighlighting || this.isLineOverlayOpen()) return;

        this.closeReader();
        break;
      case KEY_CODES.G:
        await this.goToPage();
        break;
      case KEY_CODES.F:
        this.applyFullscreen();
        break;
      case KEY_CODES.SPACE:
        this.actionBarVisible.update(x => !x);
        break;
    }
  }

  onWheel(event: WheelEvent) {
    // This allows the user to scroll the page horizontally without holding shift
    if (this.layoutMode() !== BookPageLayoutMode.Default || this.writingStyle() !== WritingStyle.Vertical) {
      return;
    }
    if (event.deltaY !== 0) {
      event.preventDefault();
      this.scrollService.scrollToX(event.deltaY + this.reader.nativeElement.scrollLeft, this.reader.nativeElement);
    }
}

  closeReader() {
    this.readerService.closeReader(this.libraryId, this.seriesId, this.chapterId, this.readingListMode, this.readingListId);
  }

  sortElements(a: Element, b: Element) {
    const aTop = a.getBoundingClientRect().top;
      const bTop = b.getBoundingClientRect().top;
      if (aTop < bTop) {
        return -1;
      }
      if (aTop > bTop) {
        return 1;
      }

      return 0;
  }

  loadImageBookmarks() {
    this.readerService.getBookmarks(this.chapterId).subscribe(res => {
      this.imageBookmarks.set(res);
      this.injectImageBookmarkIndicators(true);
    });
  }

  loadNextChapter() {
    if (this.nextPageDisabled) { return; }
    this.isLoading.set(true);

    if (this.nextChapterId === CHAPTER_ID_NOT_FETCHED || this.nextChapterId === this.chapterId) {
      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        this.loadChapter(chapterId, 'Next');
      });
      return;
    }

    this.loadChapter(this.nextChapterId, 'Next');
  }

  loadPrevChapter() {
    if (this.prevPageDisabled) { return; }

    this.isLoading.set(true);
    this.cdRef.markForCheck();
    this.continuousChaptersStack.pop();
    const prevChapter = this.continuousChaptersStack.peek();
    if (prevChapter != this.chapterId) {
      if (prevChapter !== undefined) {
        this.chapterId = prevChapter;
        this.init();
        return;
      }
    }

    if (this.prevChapterPrefetched && this.prevChapterId === CHAPTER_ID_DOESNT_EXIST) {
      this.isLoading.set(false);
      this.cdRef.markForCheck();
      return;
    }

    if (this.prevChapterId === CHAPTER_ID_NOT_FETCHED || this.prevChapterId === this.chapterId && !this.prevChapterPrefetched) {
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        this.loadChapter(chapterId, 'Prev');
      });
    } else {
      this.loadChapter(this.prevChapterId, 'Prev');
    }
  }

  loadChapter(chapterId: number, direction: 'Next' | 'Prev') {
    if (chapterId >= 0) {
      this.chapterId = chapterId;
      this.continuousChaptersStack.push(chapterId);
      // Ensure all scroll locks are undone
      this.scrollService.unlock();

      // Load chapter Id onto route but don't reload
      const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode(), this.readingListMode, this.readingListId);
      window.history.replaceState({}, '', newRoute);
      const msg = translate(direction === 'Next' ? 'toasts.load-next-chapter' : 'toasts.load-prev-chapter', {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
      this.toastr.info(msg, '', {timeOut: 3000});
      this.cdRef.markForCheck();
      this.init();
      return;
    }

    // This will only happen if no actual chapter can be found
    const msg = translate(direction === 'Next' ? 'toasts.no-next-chapter' : 'toasts.no-prev-chapter', {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
    this.toastr.warning(msg);
    this.isLoading.set(false);
    if (direction === 'Prev') {
      this.prevPageDisabled = true;
    } else {
      this.nextPageDisabled = true;
    }
    this.cdRef.markForCheck();
  }

  /**
   * Adds a click handler for any anchors that have 'kavita-page'. If 'kavita-page' present, changes page to kavita-page and optionally passes a part value
   * from 'kavita-part', which will cause the reader to scroll to the marker.
   */
  addLinkClickHandlers() {
    const links = this.readingSectionElemRef.nativeElement.querySelectorAll('a');
    links.forEach((link: any) => {
      link.addEventListener('click', (e: any) => {
        e.stopPropagation();
        let targetElem = e.target;
        if (e.target.nodeName !== 'A' && e.target.parentNode.nodeName === 'A') {
          // Certain combos like <a><sup>text</sup></a> can cause the target to be the sup tag and not the anchor
          targetElem = e.target.parentNode;
        }
        if (!targetElem.attributes.hasOwnProperty('kavita-page')) { return; }
        const page = parseInt(targetElem.attributes['kavita-page'].value, 10);
        if (this.adhocPageHistory.peek()?.page !== this.pageNum()) {
          this.adhocPageHistory.push({page: this.pageNum(), scrollPart: this.readerService.scopeBookReaderXpath(this.lastSeenScrollPartPath)});
        }

        const partValue = targetElem.attributes.hasOwnProperty('kavita-part') ? targetElem.attributes['kavita-part'].value : undefined;
        if (partValue && page === this.pageNum()) {
          this.scrollTo(targetElem.attributes['kavita-part'].value);
          return;
        }

        this.setPageNum(page);
        this.loadPage(partValue);
      });
    });
  }

  moveFocus() {
    const elems = this.document.getElementsByClassName('reading-section');
    if (elems.length > 0) {
      (elems[0] as HTMLDivElement).focus();
    }
  }

  async promptForPage() {
    const promptConfig = {...this.confirmService.defaultPrompt};
    promptConfig.header = translate('book-reader.go-to-page');
    promptConfig.content = translate('book-reader.go-to-page-prompt', {totalPages: this.maxPages()});
    promptConfig.bookReader = true;

    const goToPageNum = await this.confirmService.prompt(undefined, promptConfig);

    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  async goToPage(pageNum?: number) {
    let page = pageNum;
    if (pageNum === null || pageNum === undefined) {
      const goToPageNum = await this.promptForPage();
      if (goToPageNum === null) { return; }

      page = parseInt(goToPageNum.trim(), 10) - 1; // -1 since the UI displays with a +1
    }

    if (page === undefined || this.pageNum() === page) { return; }

    if (page > this.maxPages()) {
      page = this.maxPages();
    } else if (page < 0) {
      page = 0;
    }

    this.pageNum.set(page);
    this.loadPage();
  }

  loadPage(part?: string | undefined, scrollTop?: number | undefined) {

    this.isLoading.set(true);
    this.cdRef.markForCheck();

    this.bookService.getBookPage(this.chapterId, this.pageNum()).subscribe(content => {
      this.isSingleImagePage = this.checkSingleImagePage(content); // This needs be performed before we set this.page to avoid image jumping
      this.updateSingleImagePageStyles();

      this.page.set(this.domSanitizer.bypassSecurityTrustHtml(content));

      this.scrollService.unlock();
      this.setupObservers();

      afterFrame(() => {
        this.addLinkClickHandlers();
        this.applyPageStyles(this.pageStyles());

        const imgs = this.readingSectionElemRef.nativeElement.querySelectorAll('img');
        if (imgs !== null && imgs.length > 0) {
          Promise.all(Array.from(imgs ?? [])
            .filter(img => !img.complete)
            .map(img => new Promise(resolve => { img.onload = img.onerror = resolve; })))
            .then(() => {
              this.setupPage(part, scrollTop);
              this.updateImageSizes();
              this.injectImageBookmarkIndicators();
            });
        } else {
          this.setupPage(part, scrollTop);
        }


        this.firstLoad = false;
      });
    });
  }

  /**
   * Injects the new DOM needed to provide the bookmark functionality.
   * We can't use a wrapper due to potential for styling issues.
   */
  injectImageBookmarkIndicators(forceRefresh = false) {
    const imgs = Array.from(this.readingSectionElemRef.nativeElement.querySelectorAll('img') ?? []);

    const bookmarksForPage = (this.imageBookmarks() ?? []).filter(b => b.page === this.pageNum());

    if (forceRefresh) {
      // Remove all existing bookmark overlays
      const existingOverlays = this.readingSectionElemRef.nativeElement.querySelectorAll('.bookmark-overlay');
      existingOverlays.forEach(overlay => overlay.remove());
    }

    imgs.forEach((img, index) => {
      if (img.nextElementSibling?.classList.contains('bookmark-overlay')) return;

      const xpath = this.readerService.descopeBookReaderXpath(this.readerService.getXPathTo(img));
      const matchingBookmarks = bookmarksForPage.filter(b => b.imageOffset === index);
      let hasBookmark = matchingBookmarks.length > 0;

      const container = img.parentNode;
      if (container == null) return;

      const imgRect = img.getBoundingClientRect();
      if (imgRect.height < minImageSize.height || imgRect.width < minImageSize.width) {
        return;
      }

      const parentRect = (container as HTMLElement).getBoundingClientRect();

      const relativeX = imgRect.left - parentRect.left;
      const relativeY = imgRect.top - parentRect.top;

      const icon = document.createElement('div');
      icon.className = 'bookmark-overlay ' + (hasBookmark ? 'fa-solid' : 'fa-regular') + ' fa-bookmark';
      icon.title = hasBookmark
        ? translate('manga-reader.unbookmark-page-tooltip')
        : translate('manga-reader.bookmark-page-tooltip');

      const avgColour = this.colorscapeService.getAverageColour(img);
      let backgroundColor;
      let textColor;

      if (!avgColour || this.colorscapeService.getLuminance(avgColour) > ColorscapeService.defaultLuminanceThreshold) {
        backgroundColor = 'rgba(0, 0, 0, 0.8)';
        textColor = 'white';
      } else {
        backgroundColor = 'rgba(255, 255, 255, 1)';
        textColor = 'black';
      }

      const offSetX = Math.min(32, imgRect.width * 0.05);
      const offSetY = Math.min(32, imgRect.height * 0.05);

      icon.style.cssText = `
          position: absolute;
          left: ${imgRect.width + relativeX - offSetX}px;
          top: ${imgRect.height + relativeY - offSetY}px;
          margin: 0;
          transform-origin: bottom right;
          padding-top: 5px;
          padding-bottom: 5px;
          z-index: 1000;
          cursor: pointer;
          border-radius: 2px;
          background: ${backgroundColor} !important;
          color: ${textColor} !important;
          font-family: var(--_fa-family) !important;
        `;


      (container as HTMLElement).style.position = 'relative';
      container.appendChild(icon);

      fromEvent(icon, 'click')
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          distinctUntilChanged(),
          debounceTime(200),
          switchMap(() => hasBookmark
            ? this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum(), index)
            : this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum(), index, xpath)),
          tap(() => {
            hasBookmark = !hasBookmark;
            icon.className = 'bookmark-overlay ' + (hasBookmark ? 'fa-solid' : 'fa-regular') + ' fa-bookmark';
            this.loadImageBookmarks();
          }),
        )
        .subscribe();
    });

  }

  /**
   * Updates the image properties to fit the current layout mode and screen size
   */
  updateImageSizes() {
    const isVerticalWritingStyle = this.writingStyle() === WritingStyle.Vertical;
    const height = this.windowHeight() - (this.topOffset * 2);
    let maxHeight = 'unset';
    let maxWidth = '';
    switch (this.layoutMode()) {
      case BookPageLayoutMode.Default:
        if (isVerticalWritingStyle) {
          maxHeight = `${height}px`;
        } else {
          maxWidth = `${this.getVerticalPageWidth()}px`;
        }
        break

      case BookPageLayoutMode.Column1:
        maxHeight = `${height}px`;
        maxWidth = `${this.getVerticalPageWidth()}px`;
        break

      case BookPageLayoutMode.Column2:
        maxWidth = `${this.getVerticalPageWidth()}px`;
        if (isVerticalWritingStyle && !this.isSingleImagePage)  {
          maxHeight = `${height / 2}px`;
        } else {
          maxHeight = `${height}px`;
        }
        maxWidth = `${(this.getVerticalPageWidth() / 2) - (COLUMN_GAP / 2)}px`;
        break
    }
    this.document.documentElement.style.setProperty('--book-reader-content-max-height', maxHeight);
    this.document.documentElement.style.setProperty('--book-reader-content-max-width', maxWidth);
  }

  updateSingleImagePageStyles() {
    if (this.isSingleImagePage && this.layoutMode() !== BookPageLayoutMode.Default) {
      this.document.documentElement.style.setProperty('--book-reader-content-position', 'absolute');
      this.document.documentElement.style.setProperty('--book-reader-content-top', '50%');
      this.document.documentElement.style.setProperty('--book-reader-content-left', '50%');
      this.document.documentElement.style.setProperty('--book-reader-content-transform', 'translate(-50%, -50%)');
    } else {
      this.document.documentElement.style.setProperty('--book-reader-content-position', '');
      this.document.documentElement.style.setProperty('--book-reader-content-top', '');
      this.document.documentElement.style.setProperty('--book-reader-content-left', '');
      this.document.documentElement.style.setProperty('--book-reader-content-transform', '');
    }
  }

  checkSingleImagePage(content: string) {
    // Exclude the style element from the HTML content as it messes up innerText
    const htmlContent = content.replace(/<style>.*<\/style>/s, '');

    const parser = new DOMParser();
    const doc = parser.parseFromString(htmlContent, 'text/html');
    const html = doc.querySelector('html');

    if (html?.innerText.trim() !== '') {
      return false;
    }

    const images = doc.querySelectorAll('img, svg, image');

    return images.length === 1;
  }

  setupPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading.set(false);
    this.cdRef.markForCheck();

    // Virtual Paging stuff
    this.updateWidthAndHeightCalcs();
    this.applyLayoutMode(this.layoutMode());
    // this.addEmptyPageIfRequired(); // Already called in this.applyPageStyles()

    // Find all the part ids and their top offset
    this.setupPageAnchors();


    try {
      this.scrollWithinPage(part, scrollTop);
    } catch (ex) {
      console.error(ex);
    }

    // we need to click the document before arrow keys will scroll down.
    this.reader.nativeElement.focus();
    afterFrame(() => this.handleScrollEvent()); // Will set lastSeenXPath and save progress
    this.isLoading.set(false);
    this.cdRef.markForCheck();

    this.annotationService.getAllAnnotations(this.chapterId).subscribe(_ => {
      this.setupAnnotationElements();
    });
  }

  private scroll(lambda: () => void) {
    if (this.hasDelayedScroll) console.warn("Another scroll operation is still pending while this scroll function is being called again");
    this.hasDelayedScroll = true;

    // `afterFrame() + setTimeout()` can likely be replaced with `requestAnimationFrame()` instead
    afterFrame(() => {
      setTimeout(() => {
        this.hasDelayedScroll = false;
        lambda();
      }, SCROLL_DELAY)
    });
  }

  private scrollWithinPage(part?: string | undefined, scrollTop?: number) {
    if (part !== undefined && part !== '') {
      this.scroll(() => this.scrollTo(this.readerService.scopeBookReaderXpath(part)));
      return;
    }

    if (scrollTop !== undefined && scrollTop !== 0) {
      this.scroll(() => this.scrollService.scrollTo(scrollTop, this.reader.nativeElement));
      return;
    }

    const layoutMode = this.layoutMode();
    const writingStyle = this.writingStyle();

    if (layoutMode === BookPageLayoutMode.Default) {
      if (writingStyle === WritingStyle.Vertical) {
        //console.log('Scrolling via x axis: ', this.bookContentElemRef.nativeElement.clientWidth, ' via ', this.reader.nativeElement);
        this.scroll(() => this.scrollService.scrollToX(this.bookContentElemRef.nativeElement.clientWidth, this.reader.nativeElement));
        return;
      }

      //console.log('Scrolling via x axis to 0: ', 0, ' via ', this.reader.nativeElement);
      this.scroll(() => this.scrollService.scrollTo(0, this.reader.nativeElement));
      return;
    }

    if (writingStyle === WritingStyle.Vertical) {
      if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
        //console.log('(Vertical) Scrolling via x axis to: ', this.bookContentElemRef.nativeElement.scrollHeight, ' via ', this.bookContentElemRef.nativeElement);
        this.scroll(() => this.scrollService.scrollTo(this.bookContentElemRef.nativeElement.scrollHeight, this.bookContentElemRef.nativeElement, 'auto'));
        return;
      }

      //console.log('(Vertical) Scrolling via x axis to 0: ', 0, ' via ', this.bookContentElemRef.nativeElement);
      this.scroll(() => this.scrollService.scrollTo(0, this.bookContentElemRef.nativeElement, 'auto'));
      return;
    }

    // We need to check if we are paging back, because we need to adjust the scroll
    if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
      //console.log('(Page Back) Scrolling via x axis to: ', this.bookContentElemRef.nativeElement.scrollWidth, ' via ', this.bookContentElemRef.nativeElement);
      this.scroll(() => this.scrollService.scrollToX(this.bookContentElemRef.nativeElement.scrollWidth, this.bookContentElemRef.nativeElement));
      return;
    }

    //console.log('Scrolling via x axis to 0: ', 0, ' via ', this.bookContentElemRef.nativeElement);
    this.scroll(() => this.scrollService.scrollToX(0, this.bookContentElemRef.nativeElement));
  }

  private setupAnnotationElements() {
    this.epubHighlightService.initializeHighlightElements(this.annotationService.annotations(), this.readingContainer);
    this.cdRef.markForCheck();
  }

  private addEmptyPageIfRequired(): void {
    const bookContentElem = this.bookContentElemRef.nativeElement;
    const oldEmptyGap = bookContentElem.querySelector('.kavita-empty-gap');

    if (this.layoutMode() !== BookPageLayoutMode.Column2 || this.isSingleImagePage) {
      oldEmptyGap?.remove(); // We don't need empty gap for this condition
      return;
    }

    const pageSize = this.pageSize();
    let [_, totalScroll] = this.getScrollOffsetAndTotalScroll();

    if (oldEmptyGap) totalScroll -= pageSize/2;
    const lastPageSize = totalScroll % pageSize;

    if (lastPageSize >= pageSize / 2 || lastPageSize === 0) {
      // The last page needs more than one column, no pages will be duplicated
      oldEmptyGap?.remove();
      return;
    }

    // Need to adjust height with the column gap to ensure we don't have too much extra page
    const columnHeight = this.pageHeight() - (COLUMN_GAP * 2);
    const emptyPage = oldEmptyGap ?? this.renderer.createElement('div');
    emptyPage.classList.add('kavita-empty-gap');

    this.renderer.setStyle(emptyPage, 'height', columnHeight + 'px');
    this.renderer.setStyle(emptyPage, 'width', this.columnWidth());
    this.renderer.appendChild(bookContentElem, emptyPage);
  }

  goBack() {
    if (!this.adhocPageHistory.isEmpty()) {
      const page = this.adhocPageHistory.pop();
      if (page !== undefined) {
        this.setPageNum(page.page);
        this.loadPage(page.scrollPart);
      }
    }
  }

  setPageNum(pageNum: number) {
    this.pageNum.set(Math.max(Math.min(pageNum, this.maxPages()), 0));
  }

  /**
   * Given a direction, calls the next or prev page method
   * @param direction Direction to move
   */
  movePage(direction: PAGING_DIRECTION) {
    switch (direction) {
      case PAGING_DIRECTION.BACKWARDS:
        this.prevPage();
        break;
      case PAGING_DIRECTION.FORWARD:
        this.nextPage();
        break;
    }
  }

  prevPage() {
    const oldPageNum = this.pageNum();

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    const isColumnLayout = this.layoutMode() !== BookPageLayoutMode.Default;

    // We need to handle virtual paging before we increment the actual page
    if (isColumnLayout) {
      const [currentVirtualPage, _, pageSize] = this.getVirtualPage();

      if (currentVirtualPage > 1) {
        // Calculate the target scroll position for the previous page
        const targetScroll = (currentVirtualPage - 2) * pageSize;

        const isVertical = this.writingStyle() === WritingStyle.Vertical;

        // -2 apparently goes back 1 virtual page...
        const scrollMethod = isVertical ? 'scrollTo' : 'scrollToX';
        this.scrollService[scrollMethod](
          targetScroll,
          this.bookContentElemRef.nativeElement,
          'auto',
          () => {
            this.handleScrollEvent();
          },
          {
            tolerance: 3,
            timeout: 2000
          }
        );
        return;
      }
    }


    const newPageNum = this.pageNum() - 1;
    this.setPageNum(newPageNum);

    if (oldPageNum === 0) {
      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    if (oldPageNum === newPageNum) { return; }

    this.loadPage();
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;


    // We need to handle virtual paging before we increment the actual page
    if (this.layoutMode() !== BookPageLayoutMode.Default) {
      const [currentVirtualPage, totalVirtualPages, pageSize] = this.getVirtualPage();

      if (currentVirtualPage < totalVirtualPages) {

        // Calculate the target scroll position for the next page
        const targetScroll = (currentVirtualPage * pageSize);
        const isVertical = this.writingStyle() === WritingStyle.Vertical;

        // +0 apparently goes forward 1 virtual page...
        const scrollMethod = isVertical ? 'scrollTo' : 'scrollToX';
        this.scrollService[scrollMethod](
          targetScroll,
          this.bookContentElemRef.nativeElement,
          'auto',
          () => {
            this.handleScrollEvent();
          },
          {
            tolerance: 3,
            timeout: 2000
          }
        );
        return;
      }
    }

    const oldPageNum = this.pageNum();
    if (oldPageNum + 1 === this.maxPages()) {
      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }


    this.setPageNum(this.pageNum() + 1);
    if (oldPageNum === this.pageNum()) { return; }

    this.loadPage();
  }


  /**
   * This is the total space for the book content, excluding margin and the column gap (aka how big each column is)
   * @returns Total Page width (excluding margin)
   */
  pageWidth = computed(() => {
    this.windowWidth(); // Ensure re-compute when windows size changes (element clientWidth isn't a signal)

    const marginLeft = this.pageStyles()['margin-left'];
    const margin = (this.convertVwToPx(parseInt(marginLeft, 10)) * 2);
    const columnGapModifier = this.columnGapModifier();
    if (this.readingSectionElemRef == null) return 0;

    return this.reader.nativeElement.offsetWidth - margin + (COLUMN_GAP * columnGapModifier);
  });

  columnGapModifier = computed(() => {
    switch(this.layoutMode()) {
      case BookPageLayoutMode.Default:
        return 0;
      case BookPageLayoutMode.Column1:
      case BookPageLayoutMode.Column2:
        return 1;
    }
  });

  pageHeight = computed(() => {
    const columnHeight = this.columnHeight();
    if (this.readingSectionElemRef == null) return 0;

    const height = (parseInt(columnHeight.replace('px', ''), 10));

    return height - COLUMN_GAP;
  });


  getVerticalPageWidth = computed(() => {
    if (!(this.pageStyles() || {}).hasOwnProperty('margin-left')) return 0; // TODO: Test this, added for safety during refactor

    const margin = (this.windowWidth() * (parseInt(this.pageStyles()['margin-left'], 10) / 100)) * 2;
    const windowWidth = this.windowWidth() || document.documentElement.clientWidth;
    return windowWidth - margin;
  });

  convertVwToPx(vwValue: number) {
    const viewportWidth = Math.max(this.readingSectionElemRef?.nativeElement?.clientWidth ?? 0, window.innerWidth || 0);
    return (vwValue * viewportWidth) / 100;
  }

  /**
   * currentVirtualPage starts at 1
   * @returns currentVirtualPage, totalVirtualPages, pageSize
   */
  getVirtualPage() {
    if (!this.bookContentElemRef || !this.readingSectionElemRef) return [1, 1, 0];

    const [scrollOffset, totalScroll] = this.getScrollOffsetAndTotalScroll();
    const pageSize = this.pageSize();

    if (pageSize <= 0 || totalScroll <= 0) return [1, 1, pageSize];

    const totalVirtualPages = Math.max(1, Math.ceil(totalScroll / pageSize));
    const delta = totalScroll - scrollOffset;
    let currentVirtualPage = 1;

    //If first virtual page, i.e. totalScroll and delta are the same value
    if (totalScroll === delta) {
      currentVirtualPage = 1;
        // If second virtual page
    } else if (totalScroll - delta === pageSize) {
      currentVirtualPage = 2;
      // Otherwise do math to get correct page. i.e. scroll + pageHeight/pageWidth (this accounts for first page offset)
    } else {
      currentVirtualPage = Math.min(Math.max(1, Math.round((scrollOffset + pageSize) / pageSize)), totalVirtualPages);
    }

    return [currentVirtualPage, totalVirtualPages, pageSize];
  }

  private getScrollOffsetAndTotalScroll() {
    const { nativeElement: bookContent } = this.bookContentElemRef;
    const scrollOffset = this.writingStyle() === WritingStyle.Vertical
        ? bookContent.scrollTop
        : bookContent.scrollLeft;
    const totalScroll = this.writingStyle() === WritingStyle.Vertical
        ? bookContent.scrollHeight
        : bookContent.scrollWidth;
    return [scrollOffset, totalScroll];
  }

  pageSize = computed(() => {
    const height = this.pageHeight();
    const width = this.pageWidth();
    const writingStyle = this.writingStyle();

    return writingStyle === WritingStyle.Vertical
      ? height
      : width;
  });


  getFirstVisibleElementXPath() {
    let resumeElement: string | null = null;
    const bookContentElement = this.bookContentElemRef?.nativeElement;
    if (!bookContentElement) return null;

    //const container = this.getViewportBoundingRect();

    const intersectingEntries = Array.from(bookContentElement.querySelectorAll('div,o,p,ul,li,a,img,h1,h2,h3,h4,h5,h6,span,figure'))
      .filter(element => !element.classList.contains('no-observe'))
      .filter(element => {
        //return this.isPartiallyContainedIn(container, element);
        return this.utilityService.isInViewport(element, this.topOffset)

        /* Remove main container element
        <div class="book-content"> <-- bookContentElement
          <div class="body"> <--- we don't need this
            <style></style>
            ...
          </div>
        </div>
        */
        && element.parentElement !== bookContentElement;
      })
      .filter((element, i, entries) => {
        // Remove any children element contained in another element that exist on this entries
        return !entries.some(item => element !== item && item.contains(element))

        // Remove element that don't have any content
        && (element.textContent?.trim().length || element.querySelectorAll('img, svg').length !== 0 || /^(img|svg)$/im.test(element.tagName));
      });

    intersectingEntries.sort((a, b) => this.sortElementsForLayout(a, b));

    if (intersectingEntries.length > 0) {
      const element = this.findTopLevelElement(intersectingEntries[0], intersectingEntries[1], bookContentElement);
      let path = this.readerService.getXPathTo(element);
      if (path === '') return;

      resumeElement = path;
    }
    return resumeElement;
  }

  /**
   * Finds the top level element that has the same parent.
   * Illustrated with example below:
   *
   * <section>
   *  <p>  <-- We want to get this element instead
   *    <span> ... target ... </span>
   *  </p>
   *  <p>
   *    <span> ... nextSibling ... </span>
   *  </p>
   * <section>
  */
  private findTopLevelElement(target: Element, nextSibling: Element, root: Element): Element | null {

    // If no sibling provided, then lets transverse to parent element where the element display is not inline
    if (nextSibling == null) {
      let current: Element | null = target;
      while (current && current !== root) {
        const displayStyle = window.getComputedStyle(current).getPropertyValue('display');

        if (!displayStyle.includes('inline')) return current;
        current = current.parentElement;
      }

      return current;
    }

    // Immediately return if it's already sibling
    if (target.parentElement === nextSibling.parentElement) return target;

    const ancestors: Element[] = [];
    let current: Element | null = null

    // Collect all parent element from the next sibling
    current = nextSibling.parentElement;
    while (current && current !== root) {
      ancestors.push(current);
      current = current.parentElement;
    }

    // Traverse up from target to find the similar parent with nextSibling
    current = target;
    while (current && current !== root) {
      let parent: Element | null = current.parentElement;

      if (parent && ancestors.includes(parent)) {
        return current;
      }

      current = parent;
    }

    console.warn("Unable to find similar parent element from the next sibling", target, nextSibling);
    return target;
  }

  /**
   * Sort elements based on layout mode for better scroll position tracking
   */
  private sortElementsForLayout(a: Element, b: Element): number {
    const aRect = a.getBoundingClientRect();
    const bRect = b.getBoundingClientRect();

    switch (this.layoutMode()) {
      case BookPageLayoutMode.Default:
        return this.sortElements(a, b);
      case BookPageLayoutMode.Column1:
        return this.sortForSingleColumnLayout(a, b, aRect, bRect);
      case BookPageLayoutMode.Column2:
        return this.sortForTwoColumnLayout(a, b, aRect, bRect);
    }
  }

  /**
   * Sort for 2-column layout: prefer elements closer to the left (smaller scrollTop equivalent)
   */
  private sortForTwoColumnLayout(a: Element, b: Element, aRect: DOMRect, bRect: DOMRect): number {
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Convert horizontal position to a "reading order" score
    // Elements on the left column should be preferred over right column
    // Within the same column, prefer elements higher up

    // Determine which column each element is in
    const aColumn = aRect.left < viewportWidth / 2 ? 0 : 1; // 0 = left, 1 = right
    const bColumn = bRect.left < viewportWidth / 2 ? 0 : 1;

    // If elements are in different columns, prefer left column
    if (aColumn !== bColumn) {
      return aColumn - bColumn;
    }

    // If in the same column, prefer elements higher up (smaller top value)
    if (Math.abs(aRect.top - bRect.top) > 10) { // 10px tolerance for "same row"
      return aRect.top - bRect.top;
    }

    // If roughly at the same vertical level, prefer left-most
    return aRect.left - bRect.left;
  }

  /**
   * Sort for single column layout: prefer elements higher up
   */
  private sortForSingleColumnLayout(a: Element, b: Element, aRect: DOMRect, bRect: DOMRect): number {
    // Primary sort: vertical position (top to bottom)
    if (Math.abs(aRect.top - bRect.top) > 5) { // 5px tolerance
      return aRect.top - bRect.top;
    }

    // Secondary sort: horizontal position (left to right)
    return aRect.left - bRect.left;
  }

  /**
   * Applies styles onto the html of the book page.
   * Note: This has a critical role when margin changes and 2 column layout is in play
   */
  applyPageStyles(pageStyles: PageStyle) {
    if (this.bookContentElemRef === undefined || !this.bookContentElemRef.nativeElement) return;

    // Before we apply styles, let's get an element on the screen so we can scroll to it after any shifts
    const resumeElement: string | null | undefined = this.getFirstVisibleElementXPath();

    // Needs to update the image size when reading mode is vertically
    this.updateImageSizes();

    // Line Height must be placed on each element in the page

    // Apply page level overrides
    Object.entries(pageStyles).forEach(item => {
      if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
        // Remove the style or skip
        this.renderer.removeStyle(this.bookContentElemRef.nativeElement, item[0]);
        return;
      }
      if (pageLevelStyles.includes(item[0])) {

        let value = item[1];
        // Convert vw for margin into fixed pixels otherwise when paging, 2 column mode will bleed text between columns
        if (item[0].startsWith('margin')) {
          const vw = parseInt(item[1].replace('vw', ''), 10);
          value = `${this.convertVwToPx(vw)}px`;
        }

        this.renderer.setStyle(this.bookContentElemRef.nativeElement, item[0], value, RendererStyleFlags2.Important);
      }
    });



    const individualElementStyles = Object.entries(pageStyles).filter(item => elementLevelStyles.includes(item[0]));
    for(let i = 0; i < this.bookContentElemRef.nativeElement.children.length; i++) {
      const elem = this.bookContentElemRef.nativeElement.children.item(i);
      if (elem?.tagName === 'STYLE') continue;

      individualElementStyles.forEach(item => {
        if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
          // Remove the style or skip
          this.renderer.removeStyle(elem, item[0]);
          return;
        }
        this.renderer.setStyle(elem, item[0], item[1], RendererStyleFlags2.Important);
      });
    }

    // After layout shifts, we need to refocus the scroll bar
    // NOTE: THis is called almost always and not just from layout shift
    if (this.layoutMode() !== BookPageLayoutMode.Default && resumeElement !== null && resumeElement !== undefined) {
      this.updateWidthAndHeightCalcs();
      this.updateImageSizes(); // Re-call this as we will change window width/height again

      requestAnimationFrame(() => {
        this.addEmptyPageIfRequired(); // Try add after layout updated on next frame

        // When the user switches pages, there may be a pending scroll that moves to the start or end of the page
        // For example, `this.scrollWithinPage(...)` might be triggered when the user presses the prev/next page button
        // So, we don't need to do another page scroll here
        if (!this.hasDelayedScroll) {
          this.scrollTo(resumeElement);
        }
      });
    }
  }

  /**
   * Applies styles and classes that control theme
   * @param theme
   */
  applyColorTheme(theme: BookTheme) {
    // Remove all themes
    Array.from(this.document.querySelectorAll('style[id^="brtheme-"]')).forEach(elem => elem.remove());

    this.darkMode.set(theme.isDarkTheme);
    this.cdRef.markForCheck();

    const styleElem = this.renderer.createElement('style');
    styleElem.id = theme.selector;
    styleElem.innerHTML = theme.content;


    this.renderer.appendChild(this.document.querySelector('.reading-section'), styleElem);
    // I need to also apply the selector onto the body so that any css variables will take effect
    this.themeService.setBookTheme(theme.selector);
  }

  updateWidthAndHeightCalcs() {
    this.windowHeight.set(Math.max(this.readingSectionElemRef.nativeElement.clientHeight, window.innerHeight));
    this.windowWidth.set(Math.max(this.readingSectionElemRef.nativeElement.clientWidth, window.innerWidth));

    // Recalculate if bottom action bar is needed
    this.scrollbarNeeded.set(this.bookContentElemRef?.nativeElement?.clientHeight > this.reader?.nativeElement?.clientHeight);
    this.horizontalScrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientWidth > this.reader?.nativeElement?.clientWidth;
    this.cdRef.markForCheck();
  }

  handleReaderSettingsUpdate(res: ReaderSettingUpdate) {
    switch (res.setting) {
      case "pageStyle":
        this.applyPageStyles(res.object as PageStyle);
        break;
      case "clickToPaginate":
        this.showPaginationOverlay(res.object as boolean);
        break;
      case "fullscreen":
        this.applyFullscreen();
        break;
      case "writingStyle":
        this.applyWritingStyle();
        break;
      case "layoutMode":
        this.applyLayoutMode(res.object as BookPageLayoutMode, true);
        break;
      case "readingDirection":
        // No extra functionality needs to be done
        break;
      case "immersiveMode":
        this.applyImmersiveMode(res.object as boolean);
        break;
      case 'theme':
        this.applyColorTheme(res.object as BookTheme);
        return;
    }
  }

  toggleDrawer() {
    const drawerIsOpen = this.epubMenuService.isDrawerOpen();
    if (drawerIsOpen) {
      this.epubMenuService.closeAll();
    } else {
      this.epubMenuService.openSettingsDrawer(this.chapterId, this.seriesId, this.readingProfile, this.readerSettingsService);
    }

    if (this.immersiveMode()) { // NOTE: Shouldn't this check if drawer is open?
      this.actionBarVisible.set(false);
    }
    this.cdRef.markForCheck();
  }

  scrollTo(partSelector: string, timeout: number = 0) {
    const element = this.getElementFromXPath(partSelector) as HTMLElement;

    if (element === null) {
      if (!environment.production) {
        console.warn("Tried to scroll to a non existing XPath", partSelector);
      }

      return;
    }

    const layout = this.layoutMode();
    const writingStyle = this.writingStyle();

    if (layout !== BookPageLayoutMode.Default) {
      afterFrame(() => {
        // scrollIntoView method will only scroll to the visible area of the element (not including margin)
        // so we need to apply scroll-margin to that element to correctly scroll into it
        let margin = window.getComputedStyle(element).margin;
        if(margin !== '0px') element.style.scrollMargin = margin;

        this.scrollService.scrollIntoView(element, {timeout, scrollIntoViewOptions: {'block': 'start', 'inline': 'start'}})
      });
      return;
    }

    switch (writingStyle) {
      case WritingStyle.Vertical:
        const windowWidth = window.innerWidth || document.documentElement.clientWidth;
        const scrollLeft = element.getBoundingClientRect().left + window.scrollX - (windowWidth - element.getBoundingClientRect().width);
        afterFrame(() => this.scrollService.scrollToX(scrollLeft, this.reader.nativeElement, 'smooth'));
        break;
      case WritingStyle.Horizontal:
        const fromTopOffset = element.getBoundingClientRect().top + window.scrollY + TOP_OFFSET;
        // We need to use a delay as webkit browsers (aka Apple devices) don't always have the document rendered by this point
        afterFrame(() => this.scrollService.scrollTo(fromTopOffset, this.reader.nativeElement));
    }
  }

  getElementFromXPath(partSelector: string) {
    if (partSelector.startsWith('#')) {
      partSelector = partSelector.substring(1, partSelector.length);
    }

    let element: Element | null = null;
    if (partSelector.startsWith('//') || partSelector.startsWith('id(')) {
      // Part selector is a XPATH
      element = this.readerService.getElementFromXPath(partSelector);
    } else {
      element = this.document.querySelector('*[id="' + partSelector + '"]');
    }

    return element ?? null;
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
   turnOffIncognito() {
    this.incognitoMode.set(false);
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode(), this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info(translate('toasts.incognito-off'));
    this.saveProgress();
  }

  applyFullscreen() {
    this.isFullscreen = this.readerService.checkFullscreenMode();
    if (this.isFullscreen) {
      this.readerService.toggleFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = false;
        this.cdRef.markForCheck();
        this.renderer.removeStyle(this.reader.nativeElement, 'background');
      });
    } else {
      this.readerService.toggleFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = true;
        this.cdRef.markForCheck();
        // HACK: This is a bug with how browsers change the background color for fullscreen mode
        this.renderer.setStyle(this.reader.nativeElement, 'background', this.themeService.getCssVariable('--bs-body-color'));
        if (!this.darkMode()) {
          this.renderer.setStyle(this.reader.nativeElement, 'background', 'white');
        }
      });
    }
  }

  applyWritingStyle() {
    setTimeout(() => this.updateImageSizes());
    if (this.layoutMode() !== BookPageLayoutMode.Default) {
      const lastSelector = this.readerService.scopeBookReaderXpath(this.lastSeenScrollPartPath);
      setTimeout(() => {
        this.scrollTo(lastSelector);
      });
    } else if (this.bookContentElemRef !== undefined) {
      const resumeElement = this.getFirstVisibleElementXPath();
      if (resumeElement) {
        setTimeout(() => {
          this.scrollTo(resumeElement);
        });
      }
    }
    this.cdRef.markForCheck();
  }

  applyLayoutMode(mode: BookPageLayoutMode, isChange: boolean = false) {
    this.clearTimeout(this.updateImageSizeTimeout);
    this.updateImageSizeTimeout = setTimeout( () => {
      this.updateImageSizes();
      this.injectImageBookmarkIndicators(true);

      // This needs to be checked after the bookmark indicator has been injected or removed
      // When switching layout, these indicators may affect the page's total scrollWidth
      this.addEmptyPageIfRequired();
    }, 200);

    this.updateSingleImagePageStyles();

    // Calculate if bottom actionbar is needed. On a timeout to get accurate heights
    // if (this.bookContentElemRef == null) {
    //   setTimeout(() => this.applyLayoutMode(this.layoutMode()), 10);
    //   return;
    // }
    setTimeout(() => {
      // TODO: Why is this logic duplicated?
      this.scrollbarNeeded.set(this.bookContentElemRef?.nativeElement?.clientHeight > this.reader?.nativeElement?.clientHeight);
      this.horizontalScrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientWidth > this.reader?.nativeElement?.clientWidth;
      this.cdRef.markForCheck();
    });

    const lastSelector = this.lastSeenScrollPartPath;
    if (isChange && lastSelector !== '') {
      setTimeout(() => this.scrollTo(lastSelector), SCROLL_DELAY);
    }
  }

  applyImmersiveMode(immersiveMode: boolean) {
    if (immersiveMode && !this.epubMenuService.isDrawerOpen()) {
      this.actionBarVisible.set(false);
      this.updateReadingSectionHeight();
    }

    this.cdRef.markForCheck();
  }

  updateReadingSectionHeight() {
    const renderer = this.renderer;
    const elem = this.readingSectionElemRef;
    setTimeout(() => {
      if (renderer === undefined || elem === undefined) return;
      if (!this.immersiveMode()) {
        renderer.setStyle(elem.nativeElement, 'height', 'calc(var(--vh, 1vh) * 100 - ' + this.topOffset + 'px)', RendererStyleFlags2.Important);
      }
    });
  }

  // Table of Contents
  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  getPageMarkers(ids: Array<string>) {
    try {
      return this.document.querySelectorAll(ids.map(id => '#' + this.cleanIdSelector(id)).join(', '));
    } catch (Exception) {
      // Fallback to anchors instead. Some books have ids that are not valid for querySelectors, so anchors should be used instead
      return this.document.querySelectorAll(ids.map(id => '[href="#' + id + '"]').join(', '));
    }
  }

  setupPageAnchors() {
    this.pageAnchors = {};
    this.currentPageAnchor = '';
    this.cdRef.markForCheck();
    const ids = this.chapters.map(item => item.children).flat().filter(item => item.page === this.pageNum()).map(item => item.part).filter(item => item.length > 0);
    if (ids.length > 0) {
      const elems = this.getPageMarkers(ids);
      elems.forEach(elem => {
        this.pageAnchors[elem.id] = elem.getBoundingClientRect().top;
      });
    }
  }

  // Settings Handlers
  showPaginationOverlay(clickToPaginate: boolean) {
    this.readerSettingsService.updateClickToPaginate(clickToPaginate);
    this.cdRef.markForCheck();

    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);
    if (!clickToPaginate) { return; }

    this.clickToPaginateVisualOverlayTimeout2 = setTimeout(() => {
      this.showClickToPaginateVisualOverlay();
    }, 200);
  }

  clearTimeout(timeoutId: number | undefined) {
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
      timeoutId = undefined;
    }
  }

  showClickToPaginateVisualOverlay() {
    this.clickToPaginateVisualOverlay = true;
    this.cdRef.markForCheck();

    if (this.clickToPaginateVisualOverlay && this.clickToPaginateVisualOverlayTimeout !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout);
      this.clickToPaginateVisualOverlayTimeout = undefined;
    }
    this.clickToPaginateVisualOverlayTimeout = setTimeout(() => {
      this.clickToPaginateVisualOverlay = false;
      this.cdRef.markForCheck();
    }, 1000);

  }

  /**
   * Responsible for returning the class to show an overlay or not
   * @param side
   * @returns
   */
  clickOverlayClass(side: 'right' | 'left') {
    // TODO: See if we can use RXjs or a component to manage this aka an observable that emits the highlight to show at any given time
    if (!this.clickToPaginateVisualOverlay) {
      return '';
    }

    if (this.readingDirection() === ReadingDirection.LeftToRight) {
      return side === 'right' ? 'highlight' : 'highlight-2';
    }
    return side === 'right' ? 'highlight-2' : 'highlight';
  }

  handleReaderClick(event: MouseEvent) {
    if (!this.clickToPaginate() && !this.immersiveMode()) {
      event.preventDefault();
      event.stopPropagation();
      this.toggleMenu(event);
      return;
    }

    const isHighlighting = window.getSelection()?.toString() != '';
    if (isHighlighting) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
  }

  toggleMenu(event: MouseEvent) {
    const targetElement = (event.target as Element);
    const mouseOffset = 5;

    if (!this.immersiveMode()) return;
    if (targetElement.getAttribute('onclick') !== null || targetElement.getAttribute('href') !== null || targetElement.getAttribute('role') !== null || targetElement.getAttribute('kavita-part') != null) {
      // Don't do anything, it's actionable
      return;
    }

    if (
      Math.abs(this.mousePosition.x - event.clientX) <= mouseOffset &&
      Math.abs(this.mousePosition.y - event.clientY) <= mouseOffset
    ) {
      this.actionBarVisible.update(v => !v);
    }
  }

  mouseDown($event: MouseEvent) {
    this.mousePosition = {x: $event.clientX, y: $event.clientY};
  }

  refreshPersonalToC() {
    this.refreshPToC.emit();
  }

  updateLineOverlayOpen(isOpen: boolean) {
    this.isLineOverlayOpen.set(isOpen);
  }


  viewBookmarkImages() {
    this.epubMenuService.openViewBookmarksDrawer(this.chapterId, this.pageNum(),
      (res: PageBookmark | null, action) => {
      if (res === null) return;

      if (action === 'loadPage') {
        this.setPageNum(res.page);
        if (res.xPath != null) {
          this.loadPage(res.xPath);
        }
        return;
      } else if (action === 'removeBookmark') {
        this.loadImageBookmarks();
      }
    }, (res: LoadPageEvent) => {
        if (res === null) return;

        this.setPageNum(res.pageNumber);
        this.loadPage(res.part);
    });
  }

  async viewAnnotations() {
    await this.epubMenuService.openViewAnnotationsDrawer((annotation: Annotation) => {
      const currentPageNum = this.pageNum();
      if (this.pageNum() != annotation.pageNumber) {
        this.setPageNum(annotation.pageNumber);
      }

      if (annotation.xPath != null) {
        this.adhocPageHistory.push({page: currentPageNum, scrollPart: this.readerService.scopeBookReaderXpath(this.lastSeenScrollPartPath)});
        this.loadPage(annotation.xPath);
      }
    });
  }

  viewToCDrawer() {
    this.epubMenuService.openViewTocDrawer(this.chapterId, this.pageNum(), (res: LoadPageEvent | null) => {
      if (res === null) return;

      this.setPageNum(res.pageNumber);
      this.loadPage(res.part);
    });
  }

  /**
   * With queries and pure math, determines the actual viewport the user can see.
   *
   * NOTE: On Scroll LayoutMode, the height/bottom are not correct
   */
  getViewportBoundingRect(): Container {
    const margin = this.getMargin();
    const pageSize = this.pageWidth();
    const visibleBoundingBox = this.bookContentElemRef.nativeElement.getBoundingClientRect();

    let bookContentPadding = 20;
    let bookPadding = getComputedStyle(this.bookContentElemRef?.nativeElement!).paddingTop;
    if (bookPadding) {
      bookContentPadding = parseInt(bookPadding.toString().replace('px', ''), 10);
    }

    // Adjust the bounding box for what is actually visible
    const bottomBarHeight = this.document.querySelector('.bottom-bar')?.getBoundingClientRect().height ?? 38;
    const topBarHeight = this.document.querySelector('.fixed-top')?.getBoundingClientRect().height ?? 48;


    const left = margin;
    const top = topBarHeight;
    const bottom = visibleBoundingBox.bottom - bottomBarHeight + bookContentPadding; // bookContent has a 20px padding top/bottom
    const width = pageSize;
    const height = bottom - top;
    const right = left + width;

    // console.log('Visible Viewport', {
    //   left, right, top, bottom, width, height
    // });

    return {
      left, right, top, bottom, width, height
    }
  }

  debugInsertViewportView() {

    const viewport = this.getViewportBoundingRect();

    // Insert a debug element to help visualize
    this.document.querySelector('#test')?.remove();

    // Create and inject the red rectangle div
    const redRect = this.document.createElement('div');
    redRect.id = 'test';
    redRect.style.position = 'absolute';
    redRect.style.left = `${viewport.left}px`;
    redRect.style.top = `${viewport.top}px`;
    redRect.style.width = `${viewport.width}px`;
    redRect.style.height = `${viewport.height}px`;
    redRect.style.outline = '1px solid red';
    redRect.style.pointerEvents = 'none';
    redRect.style.zIndex = '1000';
    redRect.title = `Width: ${viewport.width}px`;

    // Inject into the document
    this.document.body.appendChild(redRect);


    // Insert margin boxes as well
    const marginLeft = this.pageStyles()['margin-left'];
    const margin = (this.convertVwToPx(parseInt(marginLeft, 10)) * 2);


    // Insert a debug element to help visualize
    this.document.querySelector('#debug-marginLeft')?.remove();

    // Create and inject the red rectangle div
    let greenRect = this.document.createElement('div');
    greenRect.id = 'debug-marginLeft';
    greenRect.style.position = 'absolute';
    greenRect.style.left = `${viewport.left - margin}px`;
    greenRect.style.top = `${viewport.top}px`;
    greenRect.style.width = `${margin}px`;
    greenRect.style.height = `${viewport.height}px`;
    greenRect.style.outline = '1px solid green';
    greenRect.style.pointerEvents = 'none';
    greenRect.style.zIndex = '1000';
    greenRect.title = `Width: ${margin}px`;

    // Inject into the document
    this.document.body.appendChild(greenRect);


    this.document.querySelector('#debug-marginRight')?.remove();

    // Create and inject the red rectangle div
    greenRect = this.document.createElement('div');
    greenRect.id = 'debug-marginRight';
    greenRect.style.position = 'absolute';
    greenRect.style.left = `${viewport.left + viewport.width}px`;
    greenRect.style.top = `${viewport.top}px`;
    greenRect.style.width = `${margin}px`;
    greenRect.style.height = `${viewport.height}px`;
    greenRect.style.outline = '1px solid green';
    greenRect.style.pointerEvents = 'none';
    greenRect.style.zIndex = '1000';
    greenRect.title = `Width: ${margin}px`;

    // Inject into the document
    this.document.body.appendChild(greenRect);

  }

  /**
   * Get actual px margin (just one side), falls back to vw -> px mapping calculation
   */
  getMargin() {
    const pageStyles = this.pageStyles();
    let usedComputed = false;
    let margin = this.convertVwToPx(parseInt(pageStyles['margin-left'], 10));


    const computedMargin = getComputedStyle(this.bookContentElemRef?.nativeElement!).marginLeft;
    if (computedMargin) {
      margin = parseInt(computedMargin.toString().replace('px', ''), 10);
      usedComputed = true;
    }

    // Sometimes computed will be 0 when first loading which can cause issues (first load)
    if (usedComputed && margin < this.convertVwToPx(parseInt(pageStyles['margin-left'], 10))) {
      console.warn('Computed margin was 0px when we expected non-zero. Defaulted back to derived vw->px value');
      return this.convertVwToPx(parseInt(pageStyles['margin-left'], 10));
    }


    return margin;
  }

  /**
   * A custom is visible method for column modes, that makes sense
   * @param container
   * @param element
   */
   isPartiallyContainedIn(container: Container, element: Element) {
    const rect = element.getBoundingClientRect();

    if (
      rect.top >= container.top &&
      rect.bottom <= container.bottom &&
      rect.right <= container.right &&
      rect.left >= container.left
    ) {
      return true;
    }

    // First element in the top, overflow to the left
    const isCloseByTop = Math.abs(rect.top - container.top) <= 0.05 * container.height;
    if (isCloseByTop && rect.left < container.left && rect.right < container.right && rect.right > container.left) {
      return true;
    }

    return false;
  }

  logSelectedElement(color='red') {
    const element = this.getElementFromXPath(this.lastSeenScrollPartPath) as HTMLElement | null;
    if (element) {
      console.log(element);
      element.style.outline = '1px solid ' + color;
      setTimeout(() => {
        element.style.outline = '';
      }, 1_000);
    }
  }


  protected readonly Breakpoint = Breakpoint;
  protected readonly environment = environment;
  protected readonly ReadingDirection = ReadingDirection;
  protected readonly PAGING_DIRECTION = PAGING_DIRECTION;
}
