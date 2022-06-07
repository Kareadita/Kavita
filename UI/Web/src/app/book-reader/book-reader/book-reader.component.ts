import { AfterViewInit, Component, ElementRef, HostListener, Inject, OnDestroy, OnInit, Renderer2, RendererStyleFlags2, ViewChild } from '@angular/core';
import {DOCUMENT, Location} from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, fromEvent, of, Subject } from 'rxjs';
import { catchError, debounceTime, take, takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { BookService } from '../book.service';
import { KEY_CODES, UtilityService } from 'src/app/shared/_services/utility.service';
import { BookChapterItem } from '../_models/book-chapter-item';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Stack } from 'src/app/shared/data-structures/stack';
import { MemberService } from 'src/app/_services/member.service';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import { MangaFormat } from 'src/app/_models/manga-format';
import { LibraryService } from 'src/app/_services/library.service';
import { LibraryType } from 'src/app/_models/library';
import { BookTheme } from 'src/app/_models/preferences/book-theme';
import { BookPageLayoutMode } from 'src/app/_models/book-page-layout-mode';
import { PageStyle } from '../reader-settings/reader-settings.component';
import { User } from 'src/app/_models/user';
import { ThemeService } from 'src/app/_services/theme.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { PAGING_DIRECTION } from 'src/app/manga-reader/_models/reader-enums';


enum TabID {
  Settings = 1,
  TableOfContents = 2
}

interface HistoryPoint {
  page: number;
  scrollOffset: number;
}

const TOP_OFFSET = -50 * 1.5; // px the sticky header takes up // TODO: Do I need this or can I change it with new fixed top height
const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

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
  animations: [
    trigger('isLoading', [
      state('false', style({opacity: 1})),
      state('true', style({opacity: 0})),
      transition('false <=> true', animate('200ms'))
    ]),
    trigger('fade', [
      state('true', style({opacity: 0})),
      state('false', style({opacity: 0.5})),
      transition('false <=> true', animate('4000ms'))
    ])
  ]
})
export class BookReaderComponent implements OnInit, AfterViewInit, OnDestroy {

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
   */
  continuousChaptersStack: Stack<number> = new Stack(); // TODO: See if continuousChaptersStack can be moved into reader service so we can reduce code duplication between readers (and also use ChapterInfo with it instead)

  /**
   * Belongs to the drawer component
   */
  activeTabId: TabID = TabID.Settings;
  /**
   * Belongs to drawer component
   */
  drawerOpen = false;
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
  /**
   * This is the html we get from the server
   */
  page: SafeHtml | undefined = undefined;
  /**
   * Next Chapter Id. This is not garunteed to be a valid ChapterId. Prefetched on page load (non-blocking).
   */
   nextChapterId: number = CHAPTER_ID_NOT_FETCHED;
   /**
    * Previous Chapter Id. This is not garunteed to be a valid ChapterId. Prefetched on page load (non-blocking).
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

  private readonly onDestroy = new Subject<void>();

  @ViewChild('readingHtml', {static: false}) readingHtml!: ElementRef<HTMLDivElement>;
  @ViewChild('readingSection', {static: false}) readingSectionElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('stickyTop', {static: false}) stickyTopElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('reader', {static: true}) reader!: ElementRef;





  get BookPageLayoutMode() {
    return BookPageLayoutMode;
  }

  get TabID(): typeof TabID {
    return TabID;
  }

  get ReadingDirection(): typeof ReadingDirection {
    return ReadingDirection;
  }

  get PAGING_DIRECTION() {
    return PAGING_DIRECTION;
  }

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
    if (this.readingHtml == null) return this.pageNum + 1 >= this.maxPages;

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
    if (this.readingHtml == null) return this.pageNum + 1 >= this.maxPages;

    return this.pageNum === 0 && (currentVirtualPage === 0);
  }

  get ColumnWidth() {
    switch (this.layoutMode) {
      case BookPageLayoutMode.Default:
        return 'unset';
      case BookPageLayoutMode.Column1:
        return (this.windowWidth /2) + 'px';
      case BookPageLayoutMode.Column2:
        return ((this.windowWidth / 4)) + 'px';
    }
  }

  get ColumnHeight() {
    if (this.layoutMode !== BookPageLayoutMode.Default) {
      // Take the height after page loads, subtract the top/bottom bar
      const height = this.windowHeight  - (this.topOffset * 2);
      this.document.documentElement.style.setProperty('--book-reader-content-max-height', `${height}px`);
      return height + 'px';
    }
    return 'unset';
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

  get PageHeightForPagination() {
    if (this.layoutMode === BookPageLayoutMode.Default) {
      return (this.readingSectionElemRef?.nativeElement?.scrollHeight || 0) - ((this.topOffset * (this.immersiveMode ? 0 : 1)) * 2) + 'px';
    }

    if (this.immersiveMode) return this.windowHeight + 'px';
    return (this.windowHeight) - (this.topOffset * 2) + 'px';
  }


  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
    private seriesService: SeriesService, private readerService: ReaderService, private location: Location,
    private renderer: Renderer2, private navService: NavService, private toastr: ToastrService,
    private domSanitizer: DomSanitizer, private bookService: BookService, private memberService: MemberService,
    private scrollService: ScrollService, private utilityService: UtilityService, private libraryService: LibraryService,
    @Inject(DOCUMENT) private document: Document, private themeService: ThemeService) {
      this.navService.hideNavBar();
      this.themeService.clearThemes();
      this.navService.hideSideNav();
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
        takeUntil(this.onDestroy))
      .subscribe((event) => {
        if (this.isLoading) return;

        this.handleScrollEvent();
    });
  }

  handleScrollEvent() {
    // Highlight the current chapter we are on
    if (Object.keys(this.pageAnchors).length !== 0) {
      // get the height of the document so we can capture markers that are halfway on the document viewport
      const verticalOffset = this.scrollService.scrollPosition + (this.document.body.offsetHeight / 2);

      const alreadyReached = Object.values(this.pageAnchors).filter((i: number) => i <= verticalOffset);
      if (alreadyReached.length > 0) {
        this.currentPageAnchor = Object.keys(this.pageAnchors)[alreadyReached.length - 1];
      } else {
        this.currentPageAnchor = '';
      }
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
      this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, tempPageNum, this.lastSeenScrollPartPath).pipe(take(1)).subscribe(() => {/* No operation */});
    }

  }

  ngOnDestroy(): void {
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout);
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);

    this.themeService.clearBookTheme();

    this.themeService.currentTheme$.pipe(take(1)).subscribe(theme => {
      this.themeService.setTheme(theme.name);
    });

    this.navService.showNavBar();
    this.navService.showSideNav();
    this.readerService.exitFullscreen();

    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');

    if (libraryId === null || seriesId === null || chapterId === null) {
      this.router.navigateByUrl('/libraries');
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


    this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(hasProgress => {
      if (!hasProgress) {
        this.toggleDrawer();
        this.toastr.info('You can modify book settings, save those settings for all books, and view table of contents from the drawer.');
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



    this.bookService.getBookInfo(this.chapterId).subscribe(info => {
      this.bookTitle = info.bookTitle;

      if (this.readingListMode && info.seriesFormat !== MangaFormat.EPUB) {
        // Redirect to the manga reader.
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(['library', info.libraryId, 'series', info.seriesId, 'manga', this.chapterId], {queryParams: params});
        return;
      }

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
        if (results.progress.bookScrollId) this.lastSeenScrollPartPath = results.progress.bookScrollId;



        this.continuousChaptersStack.push(this.chapterId);

        this.libraryService.getLibraryType(this.libraryId).pipe(take(1)).subscribe(type => {
          this.libraryType = type;
        });

        // We need to think about if the user modified this and this function call is a continuous reader one
        //this.updateLayoutMode(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default);
        this.updateImagesWithHeight();


        if (this.pageNum >= this.maxPages) {
          this.pageNum = this.maxPages - 1;
          this.saveProgress();
        }

        this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.nextChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.nextChapterDisabled = true;
            this.nextChapterPrefetched = true;
            return;
          }
          this.setPageNum(this.pageNum);
        });
        this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.prevChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.prevChapterDisabled = true;
            this.prevChapterPrefetched = true; // If there is no prev chapter, then mark it as prefetched
            return;
          }
          this.setPageNum(this.pageNum);
        });

        // Check if user progress has part, if so load it so we scroll to it
        this.loadPage(results.progress.bookScrollId || undefined);
      }, () => {
        setTimeout(() => {
          this.closeReader();
        }, 200);
      });
    });
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any){
    // Update the window Height
    this.updateWidthAndHeightCalcs();

    const resumeElement = this.getFirstVisibleElementXPath();
    if (this.layoutMode !== BookPageLayoutMode.Default && resumeElement !== null && resumeElement !== undefined) {
      this.scrollTo(resumeElement); // This works pretty well, but not perfect
    }
  }

  @HostListener('window:orientationchange', ['$event'])
  onOrientationChange() {
    // Update the window Height
    this.updateWidthAndHeightCalcs();
    const resumeElement = this.getFirstVisibleElementXPath();
    if (this.layoutMode !== BookPageLayoutMode.Default && resumeElement !== null && resumeElement !== undefined) {
      this.scrollTo(resumeElement); // This works pretty well, but not perfect
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.movePage(this.readingDirection === ReadingDirection.LeftToRight ? PAGING_DIRECTION.FORWARD : PAGING_DIRECTION.BACKWARDS);
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.movePage(this.readingDirection === ReadingDirection.LeftToRight ? PAGING_DIRECTION.BACKWARDS : PAGING_DIRECTION.FORWARD);
    } else if (event.key === KEY_CODES.ESC_KEY) {
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
      this.init();
      this.toastr.info(direction + ' ' + this.utilityService.formatChapterName(this.libraryType).toLowerCase() + ' loaded', '', {timeOut: 3000});
    } else {
      // This will only happen if no actual chapter can be found
      this.toastr.warning('Could not find ' + direction.toLowerCase() + ' ' + this.utilityService.formatChapterName(this.libraryType).toLowerCase());
      this.isLoading = false;
      if (direction === 'Prev') {
        this.prevPageDisabled = true;
      } else {
        this.nextPageDisabled = true;
      }
    }
  }

  loadChapterPage(event: {pageNum: number, part: string}) {
    this.setPageNum(event.pageNum);
    this.loadPage('id("' + event.part + '")');
  }

  closeReader() {
    if (this.readingListMode) {
      this.router.navigateByUrl('lists/' + this.readingListId);
    } else {
      this.location.back();
    }
  }


  /**
   * Adds a click handler for any anchors that have 'kavita-page'. If 'kavita-page' present, changes page to kavita-page and optionally passes a part value
   * from 'kavita-part', which will cause the reader to scroll to the marker.
   */
  addLinkClickHandlers() {
    var links = this.readingSectionElemRef.nativeElement.querySelectorAll('a');
      links.forEach((link: any) => {
        link.addEventListener('click', (e: any) => {
          if (!e.target.attributes.hasOwnProperty('kavita-page')) { return; }
          var page = parseInt(e.target.attributes['kavita-page'].value, 10);
          if (this.adhocPageHistory.peek()?.page !== this.pageNum) {
            this.adhocPageHistory.push({page: this.pageNum, scrollOffset: window.pageYOffset});
          }

          var partValue = e.target.attributes.hasOwnProperty('kavita-part') ? e.target.attributes['kavita-part'].value : undefined;
          if (partValue && page === this.pageNum) {
            this.scrollTo(e.target.attributes['kavita-part'].value);
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
    const question = 'There are ' + (this.maxPages - 1) + ' pages. What page do you want to go to?';
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

    this.bookService.getBookPage(this.chapterId, this.pageNum).pipe(take(1)).subscribe(content => {
      this.page = this.domSanitizer.bypassSecurityTrustHtml(content); // PERF: Potential optimization to prefetch next/prev page and store in localStorage

      setTimeout(() => {
        this.addLinkClickHandlers();
        this.updateReaderStyles(this.pageStyles);
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
            this.updateImagesWithHeight();
          });
      }, 10);
    });
  }

  /**
   * Applies a max-height inline css property on each image in the page if the layout mode is column-based, else it removes the property
   */
  updateImagesWithHeight() {

    const images = this.readingSectionElemRef?.nativeElement.querySelectorAll('img') || [];

    if (this.layoutMode !== BookPageLayoutMode.Default) {
      const height = (parseInt(this.ColumnHeight.replace('px', ''), 10) - (this.topOffset * 2)) + 'px';
      Array.from(images).forEach(img => {
        this.renderer.setStyle(img, 'max-height', height);
      });
    } else {
      Array.from(images).forEach(img => {
        this.renderer.removeStyle(img, 'max-height');
      });
    }
  }

  setupPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = false;

    // Virtual Paging stuff
    this.updateWidthAndHeightCalcs();
    this.updateLayoutMode(this.layoutMode || BookPageLayoutMode.Default);

    // Find all the part ids and their top offset
    this.setupPageAnchors();


    if (part !== undefined && part !== '') {
      this.scrollTo(part);
    } else if (scrollTop !== undefined && scrollTop !== 0) {
      this.scrollService.scrollTo(scrollTop, this.reader.nativeElement);
    } else {

      if (this.layoutMode === BookPageLayoutMode.Default) {
        this.scrollService.scrollTo(0, this.reader.nativeElement);
      } else {
        this.reader.nativeElement.children
        // We need to check if we are paging back, because we need to adjust the scroll
        if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
          setTimeout(() => this.scrollService.scrollToX(this.readingHtml.nativeElement.scrollWidth, this.readingHtml.nativeElement));
        } else {
          setTimeout(() => this.scrollService.scrollToX(0, this.readingHtml.nativeElement));
        }
      }  
    }

    // we need to click the document before arrow keys will scroll down.
    this.reader.nativeElement.focus();
    this.saveProgress();
  }


  goBack() {
    if (!this.adhocPageHistory.isEmpty()) {
      const page = this.adhocPageHistory.pop();
      if (page !== undefined) {
        this.setPageNum(page.page);
        this.loadPage(undefined, page.scrollOffset);
      }
    }
  }

  setPageNum(pageNum: number) {
    this.pageNum = Math.max(Math.min(pageNum, this.maxPages), 0);

    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (!this.nextChapterPrefetched && this.nextChapterId !== CHAPTER_ID_DOESNT_EXIST) { //   && !this.nextChapterDisabled
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1), catchError(err => {
          this.nextChapterDisabled = true;
          return of(null);
        })).subscribe(res => {
          this.nextChapterPrefetched = true;
        });
      }
    } else if (this.pageNum <= 10) {
      if (!this.prevChapterPrefetched && this.prevChapterId !== CHAPTER_ID_DOESNT_EXIST) { //  && !this.prevChapterDisabled
        this.readerService.getChapterInfo(this.prevChapterId).pipe(take(1), catchError(err => {
          this.prevChapterDisabled = true;
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
        this.scrollService.scrollToX((currentVirtualPage - 2) * pageWidth, this.readingHtml.nativeElement);
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
        this.scrollService.scrollToX((currentVirtualPage) * pageWidth, this.readingHtml.nativeElement);
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

    const margin = (this.readingSectionElemRef.nativeElement.clientWidth*(parseInt(this.pageStyles['margin-left'], 10) / 100))*2;
    const columnGap = 20;
    return this.readingSectionElemRef.nativeElement.clientWidth - margin + columnGap;
  }

  /**
   * currentVirtualPage starts at 1
   * @returns 
   */
  getVirtualPage() {
    if (this.readingHtml === undefined || this.readingSectionElemRef === undefined) return [1, 1, 0];

    const scrollOffset = this.readingHtml.nativeElement.scrollLeft;
    const totalScroll = this.readingHtml.nativeElement.scrollWidth;
    const pageWidth = this.getPageWidth();
    const delta = totalScroll - scrollOffset;

    const totalVirtualPages = Math.max(1, Math.round((totalScroll) / pageWidth));
    let currentVirtualPage = 1;

    // If first virtual page, i.e. totalScroll and delta are the same value
    if (totalScroll - delta === 0) {
      currentVirtualPage = 1;
    // If second virtual page
    } else if (totalScroll - delta === pageWidth) {
      currentVirtualPage = 2;

    // Otherwise do math to get correct page. i.e. scrollOffset + pageWidth (this accounts for first page offset)
    } else {
      currentVirtualPage = Math.min(Math.max(1, Math.round((scrollOffset + pageWidth) / pageWidth)), totalVirtualPages);
    } 

    return [currentVirtualPage, totalVirtualPages, pageWidth];
  }

  getFirstVisibleElementXPath() {
    let resumeElement: string | null = null;
    if (this.readingHtml === null) return null;

    const intersectingEntries = Array.from(this.readingHtml.nativeElement.querySelectorAll('div,o,p,ul,li,a,img,h1,h2,h3,h4,h5,h6,span'))
      .filter(element => !element.classList.contains('no-observe'))
      .filter(entry => {
        return this.utilityService.isInViewport(entry, this.topOffset);
      });

    intersectingEntries.sort(this.sortElements);

    if (intersectingEntries.length > 0) {
      let path = this.getXPathTo(intersectingEntries[0]);
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
    if (this.readingHtml === undefined || !this.readingHtml.nativeElement) return;

    // Before we apply styles, let's get an element on the screen so we can scroll to it after any shifts
    const resumeElement: string | null | undefined = this.getFirstVisibleElementXPath();


    // Line Height must be placed on each element in the page

    // Apply page level overrides
    Object.entries(this.pageStyles).forEach(item => {
      if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
        // Remove the style or skip
        this.renderer.removeStyle(this.readingHtml.nativeElement, item[0]);
        return;
      }
      if (pageLevelStyles.includes(item[0])) {
        this.renderer.setStyle(this.readingHtml.nativeElement, item[0], item[1], RendererStyleFlags2.Important);
      }
    });

    const individualElementStyles = Object.entries(this.pageStyles).filter(item => elementLevelStyles.includes(item[0]));
    for(let i = 0; i < this.readingHtml.nativeElement.children.length; i++) {
      const elem = this.readingHtml.nativeElement.children.item(i);
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
    this.scrollbarNeeded = this.readingHtml.nativeElement.clientHeight > this.reader.nativeElement.clientHeight;
  }

  toggleDrawer() {
    this.drawerOpen = !this.drawerOpen;

    if (this.immersiveMode) {
      this.actionBarVisible = false;
    }
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

    if (this.layoutMode === BookPageLayoutMode.Default) {
      const fromTopOffset = element.getBoundingClientRect().top + window.pageYOffset + TOP_OFFSET;
      // We need to use a delay as webkit browsers (aka apple devices) don't always have the document rendered by this point
      setTimeout(() => this.scrollService.scrollTo(fromTopOffset, this.reader.nativeElement), 10);
    } else {
      setTimeout(() => (element as Element).scrollIntoView({'block': 'start', 'inline': 'start'}));
    }
  }


  getElementFromXPath(path: string) {
    const node = this.document.evaluate(path, this.document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
    if (node?.nodeType === Node.ELEMENT_NODE) {
      return node as Element;
    }
    return null;
  }

  getXPathTo(element: any): string {
    if (element === null) return '';
    if (element.id !== '') { return 'id("' + element.id + '")'; }
    if (element === this.document.body) { return element.tagName; }


    let ix = 0;
    const siblings = element.parentNode?.childNodes || [];
    for (let sibling of siblings) {
        if (sibling === element) {
          return this.getXPathTo(element.parentNode) + '/' + element.tagName + '[' + (ix + 1) + ']';
        }
        if (sibling.nodeType === 1 && sibling.tagName === element.tagName) {
          ix++;
        }

    }
    return '';
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
      this.readerService.exitFullscreen(() => {
        this.isFullscreen = false;
        this.renderer.removeStyle(this.reader.nativeElement, 'background');
      });
    } else {
      this.readerService.enterFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = true;
        // HACK: This is a bug with how browsers change the background color for fullscreen mode
        this.renderer.setStyle(this.reader.nativeElement, 'background', this.themeService.getCssVariable('--bs-body-color')); 
        if (!this.darkMode) {
          this.renderer.setStyle(this.reader.nativeElement, 'background', 'white');
        }
      });
    }
  }

  updateLayoutMode(mode: BookPageLayoutMode) {
    this.layoutMode = mode;

    // Remove any max-heights from column layout
    this.updateImagesWithHeight();

    // Calulate if bottom actionbar is needed. On a timeout to get accurate heights
    if (this.readingHtml == null) {
      setTimeout(() => this.updateLayoutMode(this.layoutMode), 10);
      return;
    }
    setTimeout(() => {this.scrollbarNeeded = this.readingHtml.nativeElement.clientHeight > this.reader.nativeElement.clientHeight;});

    // When I switch layout, I might need to resume the progress point. 
    if (mode === BookPageLayoutMode.Default) {
      const lastSelector = this.lastSeenScrollPartPath;
      setTimeout(() => this.scrollTo(lastSelector));
    }
  }

  updateReadingDirection(readingDirection: ReadingDirection) {
    this.readingDirection = readingDirection;
  }

  updateImmersiveMode(immersiveMode: boolean) {
    this.immersiveMode = immersiveMode;
    if (this.immersiveMode && !this.drawerOpen) {
      this.actionBarVisible = false;
    }

    this.updateReadingSectionHeight();
  }

  updateReadingSectionHeight() {
    setTimeout(() => {
      console.log('setting height on ', this.readingSectionElemRef)
      if (this.immersiveMode) {
        this.renderer.setStyle(this.readingSectionElemRef, 'height', 'calc(var(--vh, 1vh) * 100)', RendererStyleFlags2.Important);
      } else {
        this.renderer.setStyle(this.readingSectionElemRef, 'height', 'calc(var(--vh, 1vh) * 100 - ' + this.topOffset + 'px)', RendererStyleFlags2.Important);
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

    if (this.clickToPaginateVisualOverlay && this.clickToPaginateVisualOverlayTimeout !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout);
      this.clickToPaginateVisualOverlayTimeout = undefined;
    }
    this.clickToPaginateVisualOverlayTimeout = setTimeout(() => {
      this.clickToPaginateVisualOverlay = false;
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



  toggleMenu(event: MouseEvent) {
    const targetElement = (event.target as Element);
    const mouseOffset = 5;

    if (!this.immersiveMode) return;
    if (targetElement.getAttribute('onclick') !== null || targetElement.getAttribute('href') !== null || targetElement.getAttribute('role') !== null || targetElement.getAttribute('kavita-part') != null) {
      // Don't do anything, it's actionable
      return;
    }

    if (
      Math.abs(this.mousePosition.x - event.screenX) <= mouseOffset &&
      Math.abs(this.mousePosition.y - event.screenY) <= mouseOffset
    ) {
      this.actionBarVisible = !this.actionBarVisible;
    }

  }

  mouseDown($event: MouseEvent) {
    this.mousePosition.x = $event.screenX;
    this.mousePosition.y = $event.screenY;
  }
}
