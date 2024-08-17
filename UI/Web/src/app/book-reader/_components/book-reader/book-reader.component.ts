import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef, EventEmitter,
  HostListener,
  inject,
  Inject,
  OnDestroy,
  OnInit,
  Renderer2,
  RendererStyleFlags2,
  ViewChild
} from '@angular/core';
import { DOCUMENT, NgTemplateOutlet, NgIf, NgStyle, NgClass } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, fromEvent, of } from 'rxjs';
import {catchError, debounceTime, distinctUntilChanged, map, take, tap} from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { CHAPTER_ID_DOESNT_EXIST, CHAPTER_ID_NOT_FETCHED, ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { BookService } from '../../_services/book.service';
import { KEY_CODES, UtilityService } from 'src/app/shared/_services/utility.service';
import { BookChapterItem } from '../../_models/book-chapter-item';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Stack } from 'src/app/shared/data-structures/stack';
import { MemberService } from 'src/app/_services/member.service';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import {WritingStyle} from "../../../_models/preferences/writing-style";
import { MangaFormat } from 'src/app/_models/manga-format';
import { LibraryService } from 'src/app/_services/library.service';
import { LibraryType } from 'src/app/_models/library/library';
import { BookTheme } from 'src/app/_models/preferences/book-theme';
import { BookPageLayoutMode } from 'src/app/_models/readers/book-page-layout-mode';
import { PageStyle, ReaderSettingsComponent } from '../reader-settings/reader-settings.component';
import { User } from 'src/app/_models/user';
import { ThemeService } from 'src/app/_services/theme.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { PAGING_DIRECTION } from 'src/app/manga-reader/_models/reader-enums';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { TableOfContentsComponent } from '../table-of-contents/table-of-contents.component';
import { NgbProgressbar, NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, NgbNavOutlet, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { DrawerComponent } from '../../../shared/drawer/drawer.component';
import {BookLineOverlayComponent} from "../book-line-overlay/book-line-overlay.component";
import {
  PersonalTableOfContentsComponent,
  PersonalToCEvent
} from "../personal-table-of-contents/personal-table-of-contents.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";


enum TabID {
  Settings = 1,
  TableOfContents = 2,
  PersonalTableOfContents = 3
}


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

const TOP_OFFSET = -50 * 1.5; // px the sticky header takes up // TODO: Do I need this or can I change it with new fixed top height

const COLUMN_GAP = 20; // px
/**
 * Styles that should be applied on the top level book-content tag
 */
const pageLevelStyles = ['margin-left', 'margin-right', 'font-size'];
/**
 * Styles that should be applied on every element within book-content tag
 */
const elementLevelStyles = ['line-height', 'font-family'];

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
    standalone: true,
  imports: [NgTemplateOutlet, DrawerComponent, NgIf, NgbProgressbar, NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink,
    NgbNavContent, ReaderSettingsComponent, TableOfContentsComponent, NgbNavOutlet, NgStyle, NgClass, NgbTooltip,
    BookLineOverlayComponent, PersonalTableOfContentsComponent, TranslocoDirective]
})
export class BookReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly seriesService = inject(SeriesService);
  private readonly readerService = inject(ReaderService);
  private readonly renderer = inject(Renderer2);
  private readonly navService = inject(NavService);
  private readonly toastr = inject(ToastrService);
  private readonly domSanitizer = inject(DomSanitizer);
  private readonly bookService = inject(BookService);
  private readonly memberService = inject(MemberService);
  private readonly scrollService = inject(ScrollService);
  private readonly utilityService = inject(UtilityService);
  private readonly libraryService = inject(LibraryService);
  private readonly themeService = inject(ThemeService);
  private readonly cdRef = inject(ChangeDetectorRef);

  protected readonly BookPageLayoutMode = BookPageLayoutMode;
  protected readonly WritingStyle = WritingStyle;
  protected readonly TabID = TabID;
  protected readonly ReadingDirection = ReadingDirection;
  protected readonly PAGING_DIRECTION = PAGING_DIRECTION;

  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;
  user!: User;

  /**
   * Reading List id. Defaults to -1.
   */
  readingListId: number = CHAPTER_ID_DOESNT_EXIST;

   /**
    * If this is true, no progress will be saved.
    */
  incognitoMode: boolean = false;

   /**
    * If this is true, chapters will be fetched in the order of a reading list, rather than natural series order.
    */
  readingListMode: boolean = false;

  /**
   * The actual pages from the epub, used for showing on table of contents. This must be here as we need access to it for scroll anchors
   */
  chapters: Array<BookChapterItem> = [];
  /**
   * Current Page
   */
  pageNum = 0;
  /**
   * Max Pages
   */
  maxPages = 1;
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
   * Belongs to the drawer component
   */
  activeTabId: TabID = TabID.Settings;
  /**
   * Sub Nav tab id
   */
  tocId: TabID = TabID.TableOfContents;
  /**
   * Belongs to drawer component
   */
  drawerOpen = false;
  /**
   * If the word/line overlay is open
   */
  isLineOverlayOpen = false;
  /**
   * If the action bar is visible
   */
  actionBarVisible = true;
  /**
   * Book reader setting that hides the menuing system
   */
  immersiveMode: boolean = false;
  /**
   * If we are loading from backend
   */
  isLoading = true;
  /**
   * Title of the book. Rendered in action bars
   */
  bookTitle: string = '';
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
  page: SafeHtml | undefined = undefined;
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
   * Internal property used to capture all the different css properties to render on all elements. This is a cached version that is updated from reader-settings component
   */
  pageStyles!: PageStyle;

  /**
   * Offset for drawer and rendering canvas. Fixed to 62px.
   */
  topOffset: number = 38;
  /**
   * Used for showing/hiding bottom action bar. Calculates if there is enough scroll to show it.
   * Will hide if all content in book is absolute positioned
   */
  horizontalScrollbarNeeded = false;
  scrollbarNeeded = false;
  readingDirection: ReadingDirection = ReadingDirection.LeftToRight;
  clickToPaginate = false;
  /**
   * Used solely for fullscreen to apply a hack
   */
  darkMode = true;
  /**
   * A anchors that map to the page number. When you click on one of these, we will load a given page up for the user.
   */
  pageAnchors: {[n: string]: number } = {};
  currentPageAnchor: string = '';
  /**
   * Last seen progress part path
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
   * How to render the page content
   */
  layoutMode: BookPageLayoutMode = BookPageLayoutMode.Default;

  /**
   * Width of the document (in non-column layout), used for column layout virtual paging
   */
  windowWidth: number = 0;
  windowHeight: number = 0;

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

  writingStyle: WritingStyle = WritingStyle.Horizontal;


  /**
   * When the user is highlighting something, then we remove pagination
   */
  hidePagination = false;

  /**
   * Used to refresh the Personal PoC
   */
  refreshPToC: EventEmitter<void> = new EventEmitter<void>();

  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('bookContainer', {static: false}) bookContainerElemRef!: ElementRef<HTMLDivElement>;
  /**
   * book-content class
   */
  @ViewChild('readingHtml', {static: false}) bookContentElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('readingSection', {static: false}) readingSectionElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('stickyTop', {static: false}) stickyTopElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('reader', {static: false}) reader!: ElementRef;

  /**
   * Disables the Left most button
   */
  get IsPrevDisabled(): boolean {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Previous button
      return this.isPrevPageDisabled();
    }

    // Acting as a Next button
    return this.isNextPageDisabled();
  }

  get IsNextDisabled(): boolean {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Next button
      return this.isNextPageDisabled();
    }
    // Acting as Previous button
    return this.isPrevPageDisabled();
  }

  isNextPageDisabled() {
    const [currentVirtualPage, totalVirtualPages, _] = this.getVirtualPage();
    const condition = (this.nextPageDisabled || this.nextChapterId === CHAPTER_ID_DOESNT_EXIST) && this.pageNum + 1 > this.maxPages - 1;
      if (this.layoutMode !== BookPageLayoutMode.Default) {
        return condition && currentVirtualPage === totalVirtualPages;
      }
      return condition;
  }

  isPrevPageDisabled() {
    const [currentVirtualPage,,] = this.getVirtualPage();
    const condition =  (this.prevPageDisabled || this.prevChapterId === CHAPTER_ID_DOESNT_EXIST) && this.pageNum === 0;
      if (this.layoutMode !== BookPageLayoutMode.Default) {
        return condition && currentVirtualPage === 0;
      }
      return condition;
  }

  /**
   * Determines if we show >> or >
   */
  get IsNextChapter(): boolean {
    if (this.layoutMode === BookPageLayoutMode.Default) {
      return this.pageNum + 1 >= this.maxPages;
    }

    const [currentVirtualPage, totalVirtualPages, _] = this.getVirtualPage();
    if (this.bookContentElemRef == null) return this.pageNum + 1 >= this.maxPages;

    return this.pageNum + 1 >= this.maxPages && (currentVirtualPage === totalVirtualPages);
  }
  /**
   * Determines if we show << or <
   */
  get IsPrevChapter(): boolean {
    if (this.layoutMode === BookPageLayoutMode.Default) {
      return this.pageNum === 0;
    }

    const [currentVirtualPage,,] = this.getVirtualPage();
    if (this.bookContentElemRef == null) return this.pageNum + 1 >= this.maxPages;

    return this.pageNum === 0 && (currentVirtualPage === 0);
  }

  get ColumnWidth() {
    const base = this.writingStyle === WritingStyle.Vertical ? this.windowHeight : this.windowWidth;
    switch (this.layoutMode) {
      case BookPageLayoutMode.Default:
        return 'unset';
      case BookPageLayoutMode.Column1:
        return ((base / 2) - 4) + 'px';
      case BookPageLayoutMode.Column2:
        return (base / 4) + 'px';
      default:
        return 'unset';
    }
  }

  get ColumnHeight() {
    if (this.layoutMode !== BookPageLayoutMode.Default || this.writingStyle === WritingStyle.Vertical) {
      // Take the height after page loads, subtract the top/bottom bar
      const height = this.windowHeight  - (this.topOffset * 2);
      return height + 'px';
    }
    return 'unset';
  }

  get VerticalBookContentWidth() {
    if (this.layoutMode !== BookPageLayoutMode.Default && this.writingStyle !== WritingStyle.Horizontal ) {
      const width = this.getVerticalPageWidth()
      return width + 'px';
    }
    return '';
  }

  get ColumnLayout() {
    switch (this.layoutMode) {
      case BookPageLayoutMode.Default:
        return '';
      case BookPageLayoutMode.Column1:
        return 'column-layout-1';
      case BookPageLayoutMode.Column2:
        return 'column-layout-2';
    }
  }

  get WritingStyleClass() {
    switch (this.writingStyle) {
        case WritingStyle.Horizontal:
          return '';
        case WritingStyle.Vertical:
            return 'writing-style-vertical';
    }
  }

  get PageWidthForPagination() {
    if (this.layoutMode === BookPageLayoutMode.Default && this.writingStyle === WritingStyle.Vertical && this.horizontalScrollbarNeeded) {
      return 'unset';
    }
    return '100%'
  }

  get PageHeightForPagination() {
    if (this.layoutMode === BookPageLayoutMode.Default) {
      // if the book content is less than the height of the container, override and return height of container for pagination area
      if (this.bookContainerElemRef?.nativeElement?.clientHeight > this.bookContentElemRef?.nativeElement?.clientHeight) {
        return (this.bookContainerElemRef?.nativeElement?.clientHeight || 0) + 'px';
      }

      return (this.bookContentElemRef?.nativeElement?.scrollHeight || 0)  - ((this.topOffset * (this.immersiveMode ? 0 : 1)) * 2) + 'px';
    }

    if (this.immersiveMode) return this.windowHeight + 'px';
    return (this.windowHeight) - (this.topOffset * 2) + 'px';
  }

  constructor(@Inject(DOCUMENT) private document: Document) {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
    this.themeService.clearThemes();
    this.cdRef.markForCheck();
  }

  /**
   * After the page has loaded, setup the scroll handler. The scroll handler has 2 parts. One is if there are page anchors setup (aka page anchor elements linked with the
   * table of content) then we calculate what has already been reached and grab the last reached one to save progress. If page anchors aren't setup (toc missing), then try to save progress
   * based on the last seen scroll part (xpath).
   */
  ngAfterViewInit() {
    // check scroll offset and if offset is after any of the "id" markers, save progress
    fromEvent(this.reader.nativeElement, 'scroll')
      .pipe(
        debounceTime(200),
        takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (this.isLoading) return;

        this.handleScrollEvent();
    });

    fromEvent<MouseEvent>(this.bookContainerElemRef.nativeElement, 'mousemove')
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        tap((e) => {
          const selection = window.getSelection();
          this.hidePagination = selection !== null && selection.toString().trim() !== '';
          this.cdRef.markForCheck();
        })
      )
      .subscribe();

    fromEvent<MouseEvent>(this.bookContainerElemRef.nativeElement, 'mouseup')
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        tap((e) => {
          this.hidePagination = false;
          this.cdRef.markForCheck();
        })
      )
      .subscribe();

  }

  handleScrollEvent() {
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
    if (xpath !== null && xpath !== undefined) this.lastSeenScrollPartPath = xpath;

    if (this.lastSeenScrollPartPath !== '') {
      this.saveProgress();
    }
  }

  saveProgress() {
    let tempPageNum = this.pageNum;
    if (this.pageNum == this.maxPages - 1) {
      tempPageNum = this.pageNum + 1;
    }

    if (!this.incognitoMode) {
      this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, this.chapterId, tempPageNum, this.lastSeenScrollPartPath).pipe(take(1)).subscribe(() => {/* No operation */});
    }
  }

  ngOnDestroy(): void {
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout);
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);

    this.readerService.disableWakeLock();

    this.themeService.clearBookTheme();

    this.themeService.currentTheme$.pipe(take(1)).subscribe(theme => {
      this.themeService.setTheme(theme.name);
    });

    this.navService.showNavBar();
    this.navService.showSideNav();
  }

  ngOnInit(): void {
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
    this.incognitoMode = this.route.snapshot.queryParamMap.get('incognitoMode') === 'true';

    const readingListId = this.route.snapshot.queryParamMap.get('readingListId');
    if (readingListId != null) {
      this.readingListMode = true;
      this.readingListId = parseInt(readingListId, 10);
    }
    this.cdRef.markForCheck();


    this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(hasProgress => {
      if (!hasProgress) {
        this.toggleDrawer();
        this.toastr.info(translate('toasts.book-settings-info'));
      }
    });

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.init();
      }
    });
  }

  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.cdRef.markForCheck();


    this.bookService.getBookInfo(this.chapterId).subscribe(info => {
      if (this.readingListMode && info.seriesFormat !== MangaFormat.EPUB) {
        // Redirect to the manga reader.
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(this.readerService.getNavigationArray(info.libraryId, info.seriesId, this.chapterId, info.seriesFormat), {queryParams: params});
        return;
      }

      this.bookTitle = info.bookTitle;
      this.cdRef.markForCheck();

      forkJoin({
        chapter: this.seriesService.getChapter(this.chapterId),
        progress: this.readerService.getProgress(this.chapterId),
        chapters: this.bookService.getBookChapters(this.chapterId),
      }).subscribe(results => {
        this.chapter = results.chapter;
        this.volumeId = results.chapter.volumeId;
        this.maxPages = results.chapter.pages;
        this.chapters = results.chapters;
        this.pageNum = results.progress.pageNum;
        this.cdRef.markForCheck();
        if (results.progress.bookScrollId) this.lastSeenScrollPartPath = results.progress.bookScrollId;

        this.continuousChaptersStack.push(this.chapterId);

        this.libraryService.getLibraryType(this.libraryId).pipe(take(1)).subscribe(type => {
          this.libraryType = type;
        });

        this.updateImageSizes();

        if (this.pageNum >= this.maxPages) {
          this.pageNum = this.maxPages - 1;
          this.cdRef.markForCheck();
          this.saveProgress();
        }

        this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.nextChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.nextChapterDisabled = true;
            this.nextChapterPrefetched = true;
            this.cdRef.markForCheck();
            return;
          }
          this.setPageNum(this.pageNum);
        });
        this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.prevChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.prevChapterDisabled = true;
            this.prevChapterPrefetched = true; // If there is no prev chapter, then mark it as prefetched
            this.cdRef.markForCheck();
            return;
          }
          this.setPageNum(this.pageNum);
        });

        // Check if user progress has part, if so load it so we scroll to it
        this.loadPage(results.progress.bookScrollId || undefined);
        this.readerService.enableWakeLock(this.reader.nativeElement);
      }, () => {
        setTimeout(() => {
          this.closeReader();
        }, 200);
      });
    });
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize(){
    // Update the window Height
    this.updateWidthAndHeightCalcs();
    this.updateImageSizes();
    const resumeElement = this.getFirstVisibleElementXPath();
    if (this.layoutMode !== BookPageLayoutMode.Default && resumeElement !== null && resumeElement !== undefined) {
      this.scrollTo(resumeElement); // This works pretty well, but not perfect
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    const activeElement = document.activeElement as HTMLElement;
    const isInputFocused = activeElement.tagName === 'INPUT' || activeElement.tagName === 'TEXTAREA';
    if (isInputFocused) return;

    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.movePage(this.readingDirection === ReadingDirection.LeftToRight ? PAGING_DIRECTION.FORWARD : PAGING_DIRECTION.BACKWARDS);
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.movePage(this.readingDirection === ReadingDirection.LeftToRight ? PAGING_DIRECTION.BACKWARDS : PAGING_DIRECTION.FORWARD);
    } else if (event.key === KEY_CODES.ESC_KEY) {
      const isHighlighting = window.getSelection()?.toString() != '';
      if (isHighlighting) return;
      this.closeReader();
    } else if (event.key === KEY_CODES.SPACE) {
      this.toggleDrawer();
      event.stopPropagation();
      event.preventDefault();
    } else if (event.key === KEY_CODES.G) {
      this.goToPage();
    } else if (event.key === KEY_CODES.F) {
      this.toggleFullscreen()
    }
  }

  onWheel(event: WheelEvent) {
    // This allows the user to scroll the page horizontally without holding shift
    if (this.layoutMode !== BookPageLayoutMode.Default || this.writingStyle !== WritingStyle.Vertical) {
      return;
    }
    if (event.deltaY !== 0) {
      event.preventDefault()
      this.scrollService.scrollToX(  event.deltaY + this.reader.nativeElement.scrollLeft, this.reader.nativeElement);
    }
}

  closeReader() {
    this.readerService.closeReader(this.readingListMode, this.readingListId);
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

  loadNextChapter() {
    if (this.nextPageDisabled) { return; }
    this.isLoading = true;
    if (this.nextChapterId === CHAPTER_ID_NOT_FETCHED || this.nextChapterId === this.chapterId) {
      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        this.loadChapter(chapterId, 'Next');
      });
    } else {
      this.loadChapter(this.nextChapterId, 'Next');
    }
  }

  loadPrevChapter() {
    if (this.prevPageDisabled) { return; }

    this.isLoading = true;
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
      this.isLoading = false;
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
      // Load chapter Id onto route but don't reload
      const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
      window.history.replaceState({}, '', newRoute);
      const msg = translate(direction === 'Next' ? 'toasts.load-next-chapter' : 'toasts.load-prev-chapter', {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
      this.toastr.info(msg, '', {timeOut: 3000});
      this.cdRef.markForCheck();
      this.init();
    } else {
      // This will only happen if no actual chapter can be found
      const msg = translate(direction === 'Next' ? 'toasts.no-next-chapter' : 'toasts.no-prev-chapter', {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
      this.toastr.warning(msg);
      this.isLoading = false;
      if (direction === 'Prev') {
        this.prevPageDisabled = true;
      } else {
        this.nextPageDisabled = true;
      }
      this.cdRef.markForCheck();
    }
  }

  loadChapterPage(event: {pageNum: number, part: string}) {
    this.setPageNum(event.pageNum);
    this.loadPage('id("' + event.part + '")');
  }

  /**
   * From personal table of contents/bookmark
   * @param event
   */
  loadChapterPart(event: PersonalToCEvent) {
    this.setPageNum(event.pageNum);
    this.loadPage(event.scrollPart);
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
          if (this.adhocPageHistory.peek()?.page !== this.pageNum) {
            this.adhocPageHistory.push({page: this.pageNum, scrollPart: this.lastSeenScrollPartPath});
          }

          const partValue = targetElem.attributes.hasOwnProperty('kavita-part') ? targetElem.attributes['kavita-part'].value : undefined;
          if (partValue && page === this.pageNum) {
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


  promptForPage() {
    const question = translate('book-reader.go-to-page-prompt', {totalPages: this.maxPages - 1});
    const goToPageNum = window.prompt(question, '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  goToPage(pageNum?: number) {
    let page = pageNum;
    if (pageNum === null || pageNum === undefined) {
      const goToPageNum = this.promptForPage();
      if (goToPageNum === null) { return; }
      page = parseInt(goToPageNum.trim(), 10);
    }

    if (page === undefined || this.pageNum === page) { return; }

    if (page > this.maxPages) {
      page = this.maxPages;
    } else if (page < 0) {
      page = 0;
    }

    if (!(page === 0 || page === this.maxPages - 1)) {
      page -= 1;
    }

    this.pageNum = page;
    this.loadPage();
  }




  loadPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.bookService.getBookPage(this.chapterId, this.pageNum).pipe(take(1)).subscribe(content => {
      this.isSingleImagePage = this.checkSingleImagePage(content) // This needs be performed before we set this.page to avoid image jumping
      this.updateSingleImagePageStyles()
      this.page = this.domSanitizer.bypassSecurityTrustHtml(content); // PERF: Potential optimization to prefetch next/prev page and store in localStorage


      this.cdRef.markForCheck();

      setTimeout(() => {
        this.addLinkClickHandlers();
        this.updateReaderStyles(this.pageStyles);

        const imgs = this.readingSectionElemRef.nativeElement.querySelectorAll('img');
        if (imgs === null || imgs.length === 0) {
          this.setupPage(part, scrollTop);
          return;
        }

        Promise.all(Array.from(imgs)
          .filter(img => !img.complete)
          .map(img => new Promise(resolve => { img.onload = img.onerror = resolve; })))
          .then(() => {
            this.setupPage(part, scrollTop);
            this.updateImageSizes();
          });
      }, 10);
    });
  }

  /**
   * Updates the image properties to fit the current layout mode and screen size
   */
  updateImageSizes() {
    const isVerticalWritingStyle = this.writingStyle === WritingStyle.Vertical;
    const height = this.windowHeight - (this.topOffset * 2);
    let maxHeight = 'unset';
    let maxWidth = '';
    switch (this.layoutMode) {
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
        break
    }
    this.document.documentElement.style.setProperty('--book-reader-content-max-height', maxHeight);
    this.document.documentElement.style.setProperty('--book-reader-content-max-width', maxWidth);

  }

  updateSingleImagePageStyles() {
    if (this.isSingleImagePage && this.layoutMode !== BookPageLayoutMode.Default) {
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

    const images = doc.querySelectorAll('img, svg');
    return images.length === 1;

  }


  setupPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = false;
    this.cdRef.markForCheck();

    // Virtual Paging stuff
    this.updateWidthAndHeightCalcs();
    this.updateLayoutMode(this.layoutMode || BookPageLayoutMode.Default);

    // Find all the part ids and their top offset
    this.setupPageAnchors();


    if (part !== undefined && part !== '') {
      this.scrollTo(part);
    } else if (scrollTop !== undefined && scrollTop !== 0) {
      this.scrollService.scrollTo(scrollTop, this.reader.nativeElement);
    } else if ((this.writingStyle === WritingStyle.Vertical) && (this.layoutMode === BookPageLayoutMode.Default)) {
       setTimeout(()=> this.scrollService.scrollToX(this.bookContentElemRef.nativeElement.clientWidth, this.reader.nativeElement));
    } else {

      if (this.layoutMode === BookPageLayoutMode.Default) {
        this.scrollService.scrollTo(0, this.reader.nativeElement);
      } else if (this.writingStyle === WritingStyle.Vertical) {
        if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
            setTimeout(() => this.scrollService.scrollTo(this.bookContentElemRef.nativeElement.scrollHeight, this.bookContentElemRef.nativeElement, 'auto'));
        } else {
            setTimeout(() => this.scrollService.scrollTo(0, this.bookContentElemRef.nativeElement,'auto' ));
        }
      }
      else {
        // We need to check if we are paging back, because we need to adjust the scroll
        if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
          setTimeout(() => this.scrollService.scrollToX(this.bookContentElemRef.nativeElement.scrollWidth, this.bookContentElemRef.nativeElement));
        } else {
          setTimeout(() => this.scrollService.scrollToX(0, this.bookContentElemRef.nativeElement));
        }
      }
    }

    // we need to click the document before arrow keys will scroll down.
    this.reader.nativeElement.focus();
    this.saveProgress();
    this.isLoading = false;
    this.cdRef.markForCheck();
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
    this.pageNum = Math.max(Math.min(pageNum, this.maxPages), 0);
    this.cdRef.markForCheck();

    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (!this.nextChapterPrefetched && this.nextChapterId !== CHAPTER_ID_DOESNT_EXIST) {
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1), catchError(err => {
          this.nextChapterDisabled = true;
          this.cdRef.markForCheck();
          return of(null);
        })).subscribe(res => {
          this.nextChapterPrefetched = true;
        });
      }
    } else if (this.pageNum <= 10) {
      if (!this.prevChapterPrefetched && this.prevChapterId !== CHAPTER_ID_DOESNT_EXIST) {
        this.readerService.getChapterInfo(this.prevChapterId).pipe(take(1), catchError(err => {
          this.prevChapterDisabled = true;
          this.cdRef.markForCheck();
          return of(null);
        })).subscribe(res => {
          this.prevChapterPrefetched = true;
        });
      }
    }
  }

  /**
   * Given a direction, calls the next or prev page method
   * @param direction Direction to move
   */
  movePage(direction: PAGING_DIRECTION) {
    if (direction === PAGING_DIRECTION.BACKWARDS) {
      this.prevPage();
      return;
    }

    this.nextPage();
  }

  prevPage() {
    const oldPageNum = this.pageNum;

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;

    // We need to handle virtual paging before we increment the actual page
    if (this.layoutMode !== BookPageLayoutMode.Default) {
      const [currentVirtualPage, _, pageWidth] = this.getVirtualPage();

      if (currentVirtualPage > 1) {
        // -2 apparently goes back 1 virtual page...
        if (this.writingStyle === WritingStyle.Vertical) {
          this.scrollService.scrollTo((currentVirtualPage - 2) * pageWidth, this.bookContentElemRef.nativeElement, 'auto');
        } else {
          this.scrollService.scrollToX((currentVirtualPage - 2) * pageWidth, this.bookContentElemRef.nativeElement);
        }
        this.handleScrollEvent();
        return;
      }
    }

    this.setPageNum(this.pageNum - 1);

    if (oldPageNum === 0) {
      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    if (oldPageNum === this.pageNum) { return; }
    this.loadPage();
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    // We need to handle virtual paging before we increment the actual page
    if (this.layoutMode !== BookPageLayoutMode.Default) {
      const [currentVirtualPage, totalVirtualPages, pageWidth] = this.getVirtualPage();

      if (currentVirtualPage < totalVirtualPages) {
        // +0 apparently goes forward 1 virtual page...
        if (this.writingStyle === WritingStyle.Vertical) {
          this.scrollService.scrollTo( (currentVirtualPage) * pageWidth, this.bookContentElemRef.nativeElement, 'auto');
        } else {
          this.scrollService.scrollToX((currentVirtualPage) * pageWidth, this.bookContentElemRef.nativeElement);
        }
        this.handleScrollEvent();
        return;
      }
    }

    const oldPageNum = this.pageNum;
    if (oldPageNum + 1 === this.maxPages) {
      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }


    this.setPageNum(this.pageNum + 1);

    if (oldPageNum === this.pageNum) { return; }

    this.loadPage();
  }

  /**
   *
   * @returns Total Page width (excluding margin)
   */
  getPageWidth() {
    if (this.readingSectionElemRef == null) return 0;
    const margin = (this.convertVwToPx(parseInt(this.pageStyles['margin-left'], 10)) * 2);
    return this.readingSectionElemRef.nativeElement.clientWidth - margin + COLUMN_GAP;
  }

  getPageHeight() {
    if (this.readingSectionElemRef == null) return 0;
    const height = (parseInt(this.ColumnHeight.replace('px', ''), 10));

    return height - COLUMN_GAP;
  }

  getVerticalPageWidth() {
    const margin = (window.innerWidth * (parseInt(this.pageStyles['margin-left'], 10) / 100)) * 2;
    const windowWidth = window.innerWidth || document.documentElement.clientWidth;
    return windowWidth - margin;
  }

  convertVwToPx(vwValue: number) {
    const viewportWidth = Math.max(this.readingSectionElemRef.nativeElement.clientWidth || 0, window.innerWidth || 0);
    return (vwValue * viewportWidth) / 100;
  }

  /**
   * currentVirtualPage starts at 1
   * @returns
   */
  getVirtualPage() {
    if (!this.bookContentElemRef || !this.readingSectionElemRef) return [1, 1, 0];

    const [scrollOffset, totalScroll] = this.getScrollOffsetAndTotalScroll();
    const pageSize = this.getPageSize();
    const totalVirtualPages = Math.max(1, Math.ceil(totalScroll / pageSize));
    const delta = scrollOffset - totalScroll;
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
    const scrollOffset = this.writingStyle === WritingStyle.Vertical
        ? bookContent.scrollTop
        : bookContent.scrollLeft;
    const totalScroll = this.writingStyle === WritingStyle.Vertical
        ? bookContent.scrollHeight
        : bookContent.scrollWidth;
    return [scrollOffset, totalScroll];
  }

  private getPageSize() {
    return this.writingStyle === WritingStyle.Vertical
        ? this.getPageHeight()
        : this.getPageWidth();
  }


  getFirstVisibleElementXPath() {
    let resumeElement: string | null = null;
    if (this.bookContentElemRef === null) return null;

    const intersectingEntries = Array.from(this.bookContentElemRef.nativeElement.querySelectorAll('div,o,p,ul,li,a,img,h1,h2,h3,h4,h5,h6,span'))
      .filter(element => !element.classList.contains('no-observe'))
      .filter(entry => {
        return this.utilityService.isInViewport(entry, this.topOffset);
      });

    intersectingEntries.sort(this.sortElements);

    if (intersectingEntries.length > 0) {
      let path = this.readerService.getXPathTo(intersectingEntries[0]);
      if (path === '') { return; }
      if (!path.startsWith('id')) {
      path = '//html[1]/' + path;
      }
      resumeElement = path;
    }
    return resumeElement;
  }

  /**
   * Applies styles onto the html of the book page
   */
  updateReaderStyles(pageStyles: PageStyle) {
    this.pageStyles = pageStyles;
    if (this.bookContentElemRef === undefined || !this.bookContentElemRef.nativeElement) return;

    // Before we apply styles, let's get an element on the screen so we can scroll to it after any shifts
    const resumeElement: string | null | undefined = this.getFirstVisibleElementXPath();

    // Needs to update the image size when reading mode is vertically
    this.updateImageSizes();

    // Line Height must be placed on each element in the page

    // Apply page level overrides
    Object.entries(this.pageStyles).forEach(item => {
      if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
        // Remove the style or skip
        this.renderer.removeStyle(this.bookContentElemRef.nativeElement, item[0]);
        return;
      }
      if (pageLevelStyles.includes(item[0])) {
        this.renderer.setStyle(this.bookContentElemRef.nativeElement, item[0], item[1], RendererStyleFlags2.Important);
      }
    });

    const individualElementStyles = Object.entries(this.pageStyles).filter(item => elementLevelStyles.includes(item[0]));
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
    if (this.layoutMode !== BookPageLayoutMode.Default && resumeElement !== null && resumeElement !== undefined) {
      this.updateWidthAndHeightCalcs();
      this.scrollTo(resumeElement); // This works pretty well, but not perfect
    }
  }

  /**
   * Applies styles and classes that control theme
   * @param theme
   */
  updateColorTheme(theme: BookTheme) {
    // Remove all themes
    Array.from(this.document.querySelectorAll('style[id^="brtheme-"]')).forEach(elem => elem.remove());

    this.darkMode = theme.isDarkTheme;

    const styleElem = this.renderer.createElement('style');
    styleElem.id = theme.selector;
    styleElem.innerHTML = theme.content;


    this.renderer.appendChild(this.document.querySelector('.reading-section'), styleElem);
    // I need to also apply the selector onto the body so that any css variables will take effect
    this.themeService.setBookTheme(theme.selector);
  }

  updateWidthAndHeightCalcs() {
    this.windowHeight = Math.max(this.readingSectionElemRef.nativeElement.clientHeight, window.innerHeight);
    this.windowWidth = Math.max(this.readingSectionElemRef.nativeElement.clientWidth, window.innerWidth);

    // Recalculate if bottom action bar is needed
    this.scrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientHeight > this.reader?.nativeElement?.clientHeight;
    this.horizontalScrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientWidth > this.reader?.nativeElement?.clientWidth;
    this.cdRef.markForCheck();
  }

  toggleDrawer() {
    this.drawerOpen = !this.drawerOpen;

    if (this.immersiveMode) {
      this.actionBarVisible = false;
    }
    this.cdRef.markForCheck();
  }

  scrollTo(partSelector: string) {
    if (partSelector.startsWith('#')) {
      partSelector = partSelector.substr(1, partSelector.length);
    }

    let element: Element | null = null;
    if (partSelector.startsWith('//') || partSelector.startsWith('id(')) {
      // Part selector is a XPATH
      element = this.getElementFromXPath(partSelector);
    } else {
      element = this.document.querySelector('*[id="' + partSelector + '"]');
    }

    if (element === null) return;

    if(this.layoutMode === BookPageLayoutMode.Default && this.writingStyle === WritingStyle.Vertical ) {
      const windowWidth = window.innerWidth || document.documentElement.clientWidth;
      const scrollLeft = element.getBoundingClientRect().left + window.pageXOffset - (windowWidth - element.getBoundingClientRect().width);
      setTimeout(() => this.scrollService.scrollToX(scrollLeft, this.reader.nativeElement, 'smooth'), 10);
    }
    else if ((this.layoutMode === BookPageLayoutMode.Default) && (this.writingStyle === WritingStyle.Horizontal)) {
      const fromTopOffset = element.getBoundingClientRect().top + window.pageYOffset + TOP_OFFSET;
      // We need to use a delay as webkit browsers (aka apple devices) don't always have the document rendered by this point
      setTimeout(() => this.scrollService.scrollTo(fromTopOffset, this.reader.nativeElement), 10);
    } else {
      setTimeout(() => (element as Element).scrollIntoView({'block': 'start', 'inline': 'start'}));
    }
  }

  getElementFromXPath(path: string) {
    const node = this.document.evaluate(path, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
    if (node?.nodeType === Node.ELEMENT_NODE) {
      return node as Element;
    }
    return null;
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
   turnOffIncognito() {
    this.incognitoMode = false;
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info('Incognito mode is off. Progress will now start being tracked.');
    this.saveProgress();
  }

  toggleFullscreen() {
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
        if (!this.darkMode) {
          this.renderer.setStyle(this.reader.nativeElement, 'background', 'white');
        }
      });
    }
  }

  updateWritingStyle(writingStyle: WritingStyle) {
    this.writingStyle = writingStyle;
    setTimeout(() => this.updateImageSizes());
    if (this.layoutMode !== BookPageLayoutMode.Default) {
      const lastSelector = this.lastSeenScrollPartPath;
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

  updateLayoutMode(mode: BookPageLayoutMode) {
    const layoutModeChanged = mode !== this.layoutMode;
    this.layoutMode = mode;
    this.cdRef.markForCheck();

    this.clearTimeout(this.updateImageSizeTimeout);
    this.updateImageSizeTimeout = setTimeout( () => {
      this.updateImageSizes()
    }, 200);

    this.updateSingleImagePageStyles()

    // Calculate if bottom actionbar is needed. On a timeout to get accurate heights
    if (this.bookContentElemRef == null) {
      setTimeout(() => this.updateLayoutMode(this.layoutMode), 10);
      return;
    }
    setTimeout(() => {
      this.scrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientHeight > this.reader?.nativeElement?.clientHeight;
      this.horizontalScrollbarNeeded = this.bookContentElemRef?.nativeElement?.clientWidth > this.reader?.nativeElement?.clientWidth;
      this.cdRef.markForCheck();
    });

    // When I switch layout, I might need to resume the progress point.
    if (mode === BookPageLayoutMode.Default && layoutModeChanged) {
      const lastSelector = this.lastSeenScrollPartPath;
      setTimeout(() => this.scrollTo(lastSelector));
    }
  }

  updateReadingDirection(readingDirection: ReadingDirection) {
    this.readingDirection = readingDirection;
    this.cdRef.markForCheck();
  }

  updateImmersiveMode(immersiveMode: boolean) {
    this.immersiveMode = immersiveMode;
    if (this.immersiveMode && !this.drawerOpen) {
      this.actionBarVisible = false;
    }
    this.updateReadingSectionHeight();
    this.cdRef.markForCheck();
  }

  updateReadingSectionHeight() {
    const renderer = this.renderer;
    const elem = this.readingSectionElemRef;
    setTimeout(() => {
      if (renderer === undefined || elem === undefined) return;
      if (this.immersiveMode) {
      } else {
        renderer.setStyle(elem, 'height', 'calc(var(--vh, 1vh) * 100 - ' + this.topOffset + 'px)', RendererStyleFlags2.Important);
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
    const ids = this.chapters.map(item => item.children).flat().filter(item => item.page === this.pageNum).map(item => item.part).filter(item => item.length > 0);
    if (ids.length > 0) {
      const elems = this.getPageMarkers(ids);
      elems.forEach(elem => {
        this.pageAnchors[elem.id] = elem.getBoundingClientRect().top;
      });
    }
  }

  // Settings Handlers
  showPaginationOverlay(clickToPaginate: boolean) {
    this.clickToPaginate = clickToPaginate;
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

    if (this.readingDirection === ReadingDirection.LeftToRight) {
      return side === 'right' ? 'highlight' : 'highlight-2';
    }
    return side === 'right' ? 'highlight-2' : 'highlight';
  }

  handleReaderClick(event: MouseEvent) {
    if (!this.clickToPaginate) {
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

    if (!this.immersiveMode) return;
    if (targetElement.getAttribute('onclick') !== null || targetElement.getAttribute('href') !== null || targetElement.getAttribute('role') !== null || targetElement.getAttribute('kavita-part') != null) {
      // Don't do anything, it's actionable
      return;
    }

    if (
      Math.abs(this.mousePosition.x - event.clientX) <= mouseOffset &&
      Math.abs(this.mousePosition.y - event.clientY) <= mouseOffset
    ) {
      this.actionBarVisible = !this.actionBarVisible;
      this.cdRef.markForCheck();
    }
  }

  mouseDown($event: MouseEvent) {
    this.mousePosition.x = $event.clientX;
    this.mousePosition.y = $event.clientY;
  }

  refreshPersonalToC() {
    this.refreshPToC.emit();
  }

  updateLineOverlayOpen(isOpen: boolean) {
    this.isLineOverlayOpen = isOpen;
    this.cdRef.markForCheck();
  }
}
