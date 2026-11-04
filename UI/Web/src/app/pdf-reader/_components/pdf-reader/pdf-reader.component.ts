import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  HostListener,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {
  IPDFViewer,
  NgxExtendedPdfViewerModule,
  NgxExtendedPdfViewerService,
  PageViewModeType,
  pdfDefaultOptions,
  PDFNotificationService,
  ProgressBarEvent,
  ScrollModeType,
  SpreadType
} from 'ngx-extended-pdf-viewer';
import {ToastrService} from '@openng/ngx-toastr';
import {auditTime, catchError, filter, forkJoin, fromEvent, merge, of, switchMap, take} from 'rxjs';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {DOCUMENT, NgStyle} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {PdfLayoutMode} from "../../../_models/preferences/pdf-layout-mode";
import {PdfScrollMode} from "../../../_models/preferences/pdf-scroll-mode";
import {PdfTheme} from "../../../_models/preferences/pdf-theme";
import {PdfSpreadMode} from "../../../_models/preferences/pdf-spread-mode";
import {PdfScrollModeTypePipe} from "../../_pipe/pdf-scroll-mode.pipe";
import {PdfSpreadTypePipe} from "../../_pipe/pdf-spread-mode.pipe";
import {ReadingProfile} from "../../../_models/preferences/reading-profiles";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {KeyBindService} from "../../../_services/key-bind.service";
import {KeyBindTarget} from "../../../_models/preferences/preferences";
import {Breakpoint, BreakpointService} from "../../../_services/breakpoint.service";
import {SeriesService} from "../../../_services/series.service";
import {NavService} from "../../../_services/nav.service";
import {BookService} from "../../../book-reader/_services/book.service";
import {BookInfo} from "../../../book-reader/_models/book-info";
import {MangaFormat} from "../../../_models/manga-format";
import {ThemeService} from "../../../_services/theme.service";
import {AccountService} from "../../../_services/account.service";
import {CHAPTER_ID_DOESNT_EXIST, CHAPTER_ID_NOT_FETCHED, ReaderService} from "../../../_services/reader.service";
import {UtilityService} from "../../../shared/_services/utility.service";
import {Stack} from "../../../shared/data-structures/stack";
import {EntityTitleService} from "../../../_services/entity-title.service";
import {LibraryService} from "../../../_services/library.service";
import {LibraryType} from "../../../_models/library/library";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ChapterPreviewComponent} from "../../../shared/_components/chapter-preview/chapter-preview.component";
import {PullToLoadComponent} from "../../../shared/_components/pull-to-load/pull-to-load.component";
import {ChapterInfo} from "../../../manga-reader/_models/chapter-info";

const KEYBIND_TARGETS = [
  {keyBindTarget: KeyBindTarget.PageLeft, description: 'prev-page'},
  {keyBindTarget: KeyBindTarget.PageRight, description: 'next-page'},
  {keyBindTarget: KeyBindTarget.OpenHelp},
  {keyBindTarget: KeyBindTarget.Escape},
  {keyBindTarget: KeyBindTarget.PreviousChapter, description: 'previous-chapter'},
  {keyBindTarget: KeyBindTarget.NextChapter, description: 'next-chapter'},
  {keyBindTarget: KeyBindTarget.FirstPage, description: 'first-page'},
  {keyBindTarget: KeyBindTarget.LastPage, description: 'last-page'},
];

const PREFETCH_PAGE_WINDOW = 10;
const END_OF_CHAPTER_CHECK_MS = 250;

// pdf.js has these, ngx's IPDFViewer typing leaves them out. Both turn by a whole spread and return false at the first/last page
type PdfJsViewer = IPDFViewer & {nextPage(): boolean, previousPage(): boolean};

const THEME_COLORS: {[key: string]: {background: string, font: string}} = {
  'dark': {'background': '#292929', 'font': '#d9d9d9'},
  'light': {'background': '#f9f9f9', 'font': '#5a5a5a'}
};

@Component({
  selector: 'app-pdf-reader',
  templateUrl: './pdf-reader.component.html',
  styleUrls: ['./pdf-reader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgStyle, NgxExtendedPdfViewerModule, NgbTooltip, TranslocoDirective,
    PdfScrollModeTypePipe, PdfSpreadTypePipe, ChapterPreviewComponent, PullToLoadComponent]
})
export class PdfReaderComponent implements OnInit, OnDestroy {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly navService = inject(NavService);
  private readonly toastr = inject(ToastrService);
  private readonly bookService = inject(BookService);
  private readonly themeService = inject(ThemeService);
  public readonly accountService = inject(AccountService);
  public readonly readerService = inject(ReaderService);
  public readonly utilityService = inject(UtilityService);
  public readonly destroyRef = inject(DestroyRef);
  public readonly document = inject(DOCUMENT);
  private readonly keyBindService = inject(KeyBindService);
  private readonly entityTitleService = inject(EntityTitleService);
  private readonly libraryService = inject(LibraryService);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly pdfViewerService = inject(NgxExtendedPdfViewerService);
  private readonly pdfNotificationService = inject(PDFNotificationService);
  protected readonly breakpointService = inject(BreakpointService);


  readonly container = viewChild.required<ElementRef>('container');
  private readonly startCap = viewChild<ElementRef<HTMLElement>>('startCap');
  private readonly endCap = viewChild<ElementRef<HTMLElement>>('endCap');

  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  protected chapterId = signal<number>(0);
  readingProfile!: ReadingProfile;
  libraryType: LibraryType = LibraryType.Manga;

  continuousChaptersStack: Stack<number> = new Stack();
  nextChapterId: number = CHAPTER_ID_NOT_FETCHED;
  prevChapterId: number = CHAPTER_ID_NOT_FETCHED;
  protected nextChapterDisabled = signal(false);
  protected prevChapterDisabled = signal(false);
  nextChapterPrefetched: boolean = false;
  prevChapterPrefetched: boolean = false;

  /**
   * Reading List id. Defaults to -1.
   */
  readingListId: number = CHAPTER_ID_DOESNT_EXIST;

  /**
   * If this is true, no progress will be saved.
   */
  protected incognitoMode = signal(false);

  /**
   * If this is true, chapters will be fetched in the order of a reading list, rather than natural series order.
   */
  readingListMode: boolean = false;

  /**
   * The page the reader sends the viewer to (open, swap, first/last page). Only bound one way to [page]: with [(page)]
   * the viewer's own reports fed back into it, and horizontal mode, which reports page N as N+1, crept forward
   */
  protected targetPage = signal(1);
  /**
   * The page the viewer reports. Drives saving, prefetch and the first/last page buttons, never moves the viewer
   */
  protected currentPage = signal(1);
  protected maxPages = signal(1);
  protected bookTitle = signal('');
  private bookInfo: BookInfo | null = null;
  private lastSavedProgress: {chapterId: number, page: number} | null = null;
  private viewerScrollWatched = false;
  /**
   * True from a document load until the reader's first key, wheel, pointer or touch. A new document reports pages
   * the reader did not turn to (a reset to page 1, a neighbour of targetPage), so nothing is saved until then
   */
  private awaitingReaderInput = false;
  /**
   * Paged forward into a fully read chapter. Its first save resets progress to 0 before saving the page, like the manga reader
   */
  private isReRead = false;
  private focusViewerOnLoad = false;

  protected prevChapter = signal<{chapterId: number, info: ChapterInfo} | null>(null);
  protected nextChapter = signal<{chapterId: number, info: ChapterInfo} | null>(null);
  protected viewerContainer = signal<HTMLElement | undefined>(undefined);
  private capsObserver: MutationObserver | null = null;
  private capsViewer: Element | null = null;

  protected zoomSetting = signal<string | number>('auto');

  protected theme = signal<'dark' | 'light'>('light');
  protected readonly backgroundColor = computed(() => THEME_COLORS[this.theme()].background);
  protected readonly fontColor = computed(() => THEME_COLORS[this.theme()].font);

  protected readonly pdfSrc = computed(() => this.readerService.downloadPdf(this.chapterId()));

  /**
   * True if Preferences.DataSaver is true
   */
  protected readonly disableLoadingIndicator = computed(() => this.accountService.userPreferences()?.dataSaver);
  protected isLoading = signal(true);
  /**
   * How much of the current document is loaded
   */
  protected loadPercent = signal(0);
  protected scrollbarNeeded = signal(false);

  protected pageLayoutMode = signal<PageViewModeType>('multiple');
  protected scrollMode = signal<ScrollModeType>(ScrollModeType.vertical);
  protected spreadMode = signal<SpreadType>('off');
  protected isSearchOpen = signal(false);

  protected readonly canDownload = computed(() =>
    this.accountService.hasDownloadRole()
  );

  private readonly capsAllowed = computed(() => !this.isLoading() && this.scrollMode() !== ScrollModeType.page);
  protected readonly showStartCap = computed(() => this.capsAllowed() && this.prevChapter() !== null);
  protected readonly showEndCap = computed(() => this.capsAllowed()
    && (this.nextChapter() !== null || this.nextChapterDisabled()));

  constructor() {
      this.navService.hideNavBar();
      this.themeService.clearThemes();
      this.navService.hideSideNav();

      this.keyBindService.registerListener(
        this.destroyRef,
        (e) => {
          switch (e.target) {
            case KeyBindTarget.Escape:
              this.exitReader();
              break;
            case KeyBindTarget.PageRight:
              this.nextPage();
              break;
            case KeyBindTarget.PageLeft:
              this.prevPage();
              break;
            case KeyBindTarget.NextChapter:
              this.loadNextChapter();
              break;
            case KeyBindTarget.PreviousChapter:
              this.loadPrevChapter();
              break;
            case KeyBindTarget.FirstPage:
              this.goToPage(1);
              break;
            case KeyBindTarget.LastPage:
              this.goToPage(this.maxPages());
              break;
            case KeyBindTarget.OpenHelp:
              this.openShortcutModal();
          }

        },
        KEYBIND_TARGETS.map(k => k.keyBindTarget as KeyBindTarget),
      );

      effect(() => {
        const prefs = this.accountService.userPreferences();
        if (prefs) {
          pdfDefaultOptions.disableAutoFetch = prefs.dataSaver;
        }
      });
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize(event: Event){
    // Update the window Height
    this.calcScrollbarNeeded();
  }

  ngOnDestroy(): void {
    this.themeService.currentTheme$.pipe(take(1)).subscribe(theme => {
      this.themeService.setTheme(theme.name);
    });

    this.navService.showNavBar();
    this.navService.showSideNav();
    this.readerService.disableWakeLock();
    this.capsObserver?.disconnect();

    window.removeEventListener('keydown', this.downloadHandler, { capture: true });
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
    this.chapterId.set(parseInt(chapterId, 10));
    this.incognitoMode.set(this.route.snapshot.queryParamMap.get('incognitoMode') === 'true');

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.readingProfile = data['readingProfile'];
      if (this.readingProfile == null) {
        this.router.navigateByUrl('/home');
        return;
      }
      this.setupReaderSettings();
    });


    const readingListId = this.route.snapshot.queryParamMap.get('readingListId');
    if (readingListId != null) {
      this.readingListMode = true;
      this.readingListId = parseInt(readingListId, 10);
    }

    window.addEventListener('keydown', this.downloadHandler, { capture: true });

    // Any input means that the page reports from here on, are the reader's own. see awaitingReaderInput
    merge(
      fromEvent(this.document, 'keydown'),
      fromEvent(this.document, 'wheel', {passive: true}),
      fromEvent(this.document, 'pointerdown'),
      fromEvent(this.document, 'touchstart', {passive: true}),
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.awaitingReaderInput = false);

    this.continuousChaptersStack.push(this.chapterId());
    this.init(true);
    setTimeout(() => this.readerService.enableWakeLock(this.container().nativeElement), 1000);
  }

  private downloadHandler = (event: KeyboardEvent) => {
    if (event.ctrlKey && event.key.toLowerCase() === 's') {
      if (!this.accountService.hasDownloadRole()) {
        event.preventDefault();
        event.stopImmediatePropagation(); // Stops ALL other handlers
      }
    }
  };


  calcScrollbarNeeded() {
    const viewContainer = this.document.querySelector('#viewerContainer');
    if (viewContainer == null) return;
    this.scrollbarNeeded.set(viewContainer.scrollHeight > this.container()?.nativeElement?.clientHeight);
  }

  convertPdfLayoutMode(mode: PdfLayoutMode) {
    switch (mode) {
      case PdfLayoutMode.Multiple:
        return 'multiple';
      case PdfLayoutMode.Single:
        return 'single';
      case PdfLayoutMode.Book:
        return 'book';
      case PdfLayoutMode.InfiniteScroll:
        return 'infinite-scroll';

    }
  }

  convertPdfScrollMode(mode: PdfScrollMode) {
    switch (mode) {
      case PdfScrollMode.Vertical:
        return ScrollModeType.vertical;
      case PdfScrollMode.Horizontal:
        return ScrollModeType.horizontal;
      case PdfScrollMode.Wrapped:
        return ScrollModeType.wrapped;
      case PdfScrollMode.Page:
        return ScrollModeType.page;
    }
  }

  convertPdfSpreadMode(mode: PdfSpreadMode): SpreadType {
    switch (mode) {
      case PdfSpreadMode.None:
        return 'off' as SpreadType;
      case PdfSpreadMode.Odd:
        return 'odd' as SpreadType;
      case PdfSpreadMode.Even:
        return 'even' as SpreadType;
    }
  }

  convertPdfTheme(theme: PdfTheme) {
    switch (theme) {
      case PdfTheme.Dark:
        return 'dark';
      case PdfTheme.Light:
        return 'light';
    }
  }

  setupReaderSettings() {
    this.pageLayoutMode.set(this.convertPdfLayoutMode(PdfLayoutMode.Multiple));
    this.scrollMode.set(this.convertPdfScrollMode(this.readingProfile.pdfScrollMode || PdfScrollMode.Vertical));
    this.spreadMode.set(this.convertPdfSpreadMode(this.readingProfile.pdfSpreadMode || PdfSpreadMode.None));
    this.theme.set(this.convertPdfTheme(this.readingProfile.pdfTheme || PdfTheme.Dark));
  }

  init(firstLoad: boolean, direction: 'Next' | 'Prev' | 'None' = 'None') {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled.set(false);
    this.prevChapterDisabled.set(false);
    this.nextChapterPrefetched = false;
    this.prevChapterPrefetched = false;
    this.isReRead = false;
    this.prevChapter.set(null);
    this.nextChapter.set(null);
    this.calcScrollbarNeeded();

    const chapterId = this.chapterId();
    forkJoin({
      info: this.bookService.getBookInfo(chapterId),
      progress: this.readerService.getProgress(chapterId),
      chapter: this.seriesService.getChapter(chapterId),
    }).subscribe({
      next: ({info, progress, chapter}) => {
        this.bookInfo = info;
        this.seriesId = info.seriesId;
        this.libraryId = info.libraryId;
        this.volumeId = info.volumeId;
        this.bookTitle.set(info.bookTitle);
        this.maxPages.set(chapter.pages);
        this.isReRead = direction === 'Next' && progress.pageNum >= chapter.pages;
        const startingPage = this.getStartingPage(progress.pageNum, direction);
        this.awaitingReaderInput = true;
        this.targetPage.set(startingPage);
        this.currentPage.set(startingPage);

        if (!firstLoad) {
          this.replaceReaderUrl();
        }

        this.libraryService.getLibraryType(this.libraryId).subscribe(type => this.libraryType = type);
        this.fetchAdjacentChapters(chapterId);
      },
      error: () => {
        setTimeout(() => this.closeReader(), 200);
      }
    });
  }

  private getStartingPage(savedPage: number, direction: 'Next' | 'Prev' | 'None') {
    if (savedPage < this.maxPages()) return Math.max(savedPage, 1);
    return direction === 'Next' ? 1 : this.maxPages();
  }

  private fetchAdjacentChapters(chapterId: number) {
    this.readerService.getNextChapter(this.seriesId, this.volumeId, chapterId, this.readingListId).subscribe(nextId => {
      if (chapterId !== this.chapterId()) return;
      this.nextChapterId = nextId;
      this.nextChapterDisabled.set(nextId === CHAPTER_ID_DOESNT_EXIST || nextId === chapterId);
      this.prefetchAdjacentChapters();
    });
    this.readerService.getPrevChapter(this.seriesId, this.volumeId, chapterId, this.readingListId).subscribe(prevId => {
      if (chapterId !== this.chapterId()) return;
      this.prevChapterId = prevId;
      this.prevChapterDisabled.set(prevId === CHAPTER_ID_DOESNT_EXIST || prevId === chapterId);
      this.prefetchAdjacentChapters();
    });
  }

  private prefetchAdjacentChapters() {
    const currentPage = this.currentPage();
    if (!this.nextChapterPrefetched && this.nextChapterId > 0 && currentPage >= this.maxPages() - PREFETCH_PAGE_WINDOW) {
      this.nextChapterPrefetched = true;
      const nextChapterId = this.nextChapterId;
      this.readerService.getChapterInfo(nextChapterId).pipe(catchError(() => of(null))).subscribe(info => {
        if (!info || nextChapterId !== this.nextChapterId) return;
        this.nextChapter.set({chapterId: nextChapterId, info});
      });
    }
    if (!this.prevChapterPrefetched && this.prevChapterId > 0 && currentPage <= PREFETCH_PAGE_WINDOW) {
      this.prevChapterPrefetched = true;
      const prevChapterId = this.prevChapterId;
      this.readerService.getChapterInfo(prevChapterId).pipe(catchError(() => of(null))).subscribe(info => {
        if (!info || prevChapterId !== this.prevChapterId) return;
        this.prevChapter.set({chapterId: prevChapterId, info});
      });
    }
  }

  private replaceReaderUrl() {
    this.readerService.replaceReaderUrl(this.libraryId, this.seriesId, this.chapterId(), MangaFormat.PDF,
      this.incognitoMode(), this.readingListMode, this.readingListId);
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
   turnOffIncognito() {
    this.incognitoMode.set(false);
    this.replaceReaderUrl();
    this.toastr.info(translate('toasts.incognito-off'));
    this.saveProgress();
  }

  toggleTheme() {
    this.theme.update(theme => theme === 'dark' ? 'light' : 'dark');
  }

  toggleScrollMode() {
    const options: Array<ScrollModeType> = [ScrollModeType.vertical, ScrollModeType.horizontal, ScrollModeType.page];
    let index = options.indexOf(this.scrollMode()) + 1;
    if (index >= options.length) index = 0;
    this.scrollMode.set(options[index]);

    this.calcScrollbarNeeded();
    const currPage = this.currentPage();

    setTimeout(() => {
      this.goToPage(currPage);
    }, 100);
  }

  toggleSpreadMode() {
     const options: Array<SpreadType> = ['off', 'odd', 'even'];
     let index = options.indexOf(this.spreadMode()) + 1;
     if (index >= options.length) index = 0;
     this.spreadMode.set(options[index]);
  }

  toggleBookPageMode() {
    if (this.pageLayoutMode() === 'book') {
      this.pageLayoutMode.set('multiple');
    } else {
      if (this.breakpointService.activeBreakpoint() < Breakpoint.Tablet) {
        this.toastr.info(translate('toasts.pdf-book-mode-screen-size'));
        return;
      }
      this.pageLayoutMode.set('book');
      // If the fit is automatic, let's adjust to 100% to ensure it renders correctly (can't do this, but it doesn't always happen)
    }
  }

  onPageChange(page: number | undefined) {
    if (page === undefined) return;
    this.currentPage.set(page);
    this.prefetchAdjacentChapters();

    // A new document reports a page near the one it was sent to, not that page (horizontal reports 62 with 62-64 on screen)
    if (this.awaitingReaderInput) {
      // Horizontal mode also resets a new document to its first page after [page] was applied, and does not reapply it
      if (!this.pdfViewerService.getCurrentlyVisiblePageNumbers().includes(this.targetPage())) {
        this.pdfViewerService.scrollPageIntoView(this.targetPage());
      }
      return;
    }

    this.saveProgress();
  }

  protected goToPage(page: number) {
    this.targetPage.set(page);
    // [page] is only re-pushed when targetPage changes, and the viewer may have scrolled away from it
    this.pdfViewerService.scrollPageIntoView(page);
  }

  saveProgress() {
    if (this.incognitoMode() || !this.bookInfo) return;

    const chapterId = this.chapterId();
    const page = this.isLastPageEndInView() ? this.maxPages() : this.currentPage();
    if (this.lastSavedProgress?.chapterId === chapterId && this.lastSavedProgress.page === page) return;

    const saved = {chapterId, page};
    this.lastSavedProgress = saved;
    const save$ = this.isReRead
      ? this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, chapterId, 0).pipe(
          switchMap(() => this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, chapterId, page)))
      : this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, chapterId, page);
    this.isReRead = false;

    save$.subscribe({
      error: () => {
        if (this.lastSavedProgress === saved) {
          this.lastSavedProgress = null;
        }
      }
    });
  }

  onPdfLoaded() {
    this.isLoading.set(false);
    this.watchViewerScroll();
    this.keepChapterCapsInViewer();

    // Loading hides the caps, which drops a focused card's focus to <body>
    if (this.focusViewerOnLoad) {
      this.focusViewerOnLoad = false;
      this.viewerContainer()?.focus({preventScroll: true});
    }
  }

  private isFocusInChapterCap() {
    const active = this.document.activeElement;
    if (!active) return false;
    return !!(this.startCap()?.nativeElement.contains(active) || this.endCap()?.nativeElement.contains(active));
  }

  // pdf.js empties #viewer for every new document and spread change, which takes the caps with it
  private keepChapterCapsInViewer() {
    const viewer = this.document.querySelector('#viewer');
    const startCap = this.startCap()?.nativeElement;
    const endCap = this.endCap()?.nativeElement;
    if (!viewer || !startCap || !endCap || viewer === this.capsViewer) return;

    this.viewerContainer.set(viewer.parentElement ?? undefined);
    // Without a tabindex a div only takes focus while it overflows, and never in Safari
    viewer.parentElement?.setAttribute('tabindex', '-1');
    this.capsObserver?.disconnect();
    this.capsViewer = viewer;

    const placeCaps = () => {
      if (viewer.firstElementChild !== startCap) {
        viewer.prepend(startCap);
      }
      if (viewer.lastElementChild !== endCap) {
        viewer.append(endCap);
      }
    };
    this.capsObserver = new MutationObserver(placeCaps);
    this.capsObserver.observe(viewer, {childList: true});
    placeCaps();
  }

  // pageChange stops firing near the end: with pages 278-282 on screen the viewer stays on 279, so the end is caught from scroll
  private watchViewerScroll() {
    if (this.viewerScrollWatched) return;
    const viewer = this.document.querySelector('#viewerContainer');
    if (!viewer) return;

    this.viewerScrollWatched = true;
    fromEvent(viewer, 'scroll', {passive: true}).pipe(
      auditTime(END_OF_CHAPTER_CHECK_MS),
      filter(() => !this.awaitingReaderInput && this.isLastPageEndInView()),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => this.saveProgress());
  }

  exitReader() {
    if (this.readerService.closeRateReviewModal() || this.readerService.closeShortCutModal()) return;

    if (!this.bookInfo || !this.isLastPageEndInView()) {
      this.closeReader();
      return;
    }

    this.saveProgress();
    this.promptRateAndReview(this.bookInfo).subscribe(() => this.closeReader());
  }

  private promptRateAndReview(bookInfo: BookInfo) {
    return this.readerService.promptRateAndReview({
      seriesId: bookInfo.seriesId,
      volumeId: bookInfo.volumeId,
      chapterId: this.chapterId(),
      seriesName: bookInfo.seriesName,
      publicationStatus: bookInfo.seriesPublicationStatus,
      readingListMode: this.readingListMode,
    });
  }

  loadNextChapter() {
    if (!this.bookInfo) return;

    if (this.isLastPageEndInView()) {
      this.saveProgress();
    }

    if (this.nextChapterDisabled()) {
      this.handleNoNextChapter(this.bookInfo);
      return;
    }

    if (this.nextChapterId === CHAPTER_ID_NOT_FETCHED) {
      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId(), this.readingListId).subscribe(nextId => {
        this.nextChapterId = nextId;
        this.loadChapter(nextId, 'Next');
      });
      return;
    }

    this.loadChapter(this.nextChapterId, 'Next');
  }

  loadPrevChapter() {
    if (!this.bookInfo) return;

    if (this.prevChapterDisabled()) {
      this.toastr.warning(translate('toasts.no-prev-chapter', {entity: this.chapterEntityName()}));
      return;
    }

    this.continuousChaptersStack.pop();
    const stackedChapterId = this.continuousChaptersStack.peek();
    if (stackedChapterId !== undefined && stackedChapterId !== this.chapterId()) {
      this.swapToChapter(stackedChapterId, 'Prev');
      return;
    }

    if (this.prevChapterId === CHAPTER_ID_NOT_FETCHED) {
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId(), this.readingListId).subscribe(prevId => {
        this.prevChapterId = prevId;
        this.loadChapter(prevId, 'Prev');
      });
      return;
    }

    this.loadChapter(this.prevChapterId, 'Prev');
  }

  private loadChapter(chapterId: number, direction: 'Next' | 'Prev') {
    if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId()) {
      if (direction === 'Next') {
        this.nextChapterDisabled.set(true);
        if (this.bookInfo) {
          this.handleNoNextChapter(this.bookInfo);
        }
      } else {
        this.prevChapterDisabled.set(true);
        this.toastr.warning(translate('toasts.no-prev-chapter', {entity: this.chapterEntityName()}));
      }
      return;
    }

    this.continuousChaptersStack.push(chapterId);
    this.swapToChapter(chapterId, direction);
    const key = direction === 'Next' ? 'toasts.load-next-chapter' : 'toasts.load-prev-chapter';
    this.toastr.info(translate(key, {entity: this.chapterEntityName()}), '', {timeOut: 3000});
  }

  private swapToChapter(chapterId: number, direction: 'Next' | 'Prev') {
    const currentBookInfo = this.bookInfo;
    this.bookInfo = null;
    this.focusViewerOnLoad = this.isFocusInChapterCap();
    this.isLoading.set(true);

    // The viewer must never get a non-PDF as src: it goes blank, with the error only in the console
    this.bookService.getBookInfo(chapterId).subscribe({
      next: info => {
        if (info.seriesFormat !== MangaFormat.PDF) {
          const params = this.readerService.getQueryParamsObject(this.incognitoMode(), this.readingListMode, this.readingListId);
          this.router.navigate(this.readerService.getNavigationArray(info.libraryId, info.seriesId, chapterId, info.seriesFormat), {queryParams: params});
          return;
        }

        // Before src changes: the viewer opens a document in the scroll mode it holds, then reports that mode back
        const readingProfile$ = info.seriesId === this.seriesId
          ? of(null)
          : this.readingProfileService.getForSeries(info.libraryId, info.seriesId).pipe(catchError(() => of(null)));
        readingProfile$.subscribe(readingProfile => {
          if (readingProfile) {
            this.readingProfile = readingProfile;
            this.setupReaderSettings();
            this.readerService.showReadingProfileSwitch(readingProfile, info.seriesName);
          }

          this.chapterId.set(chapterId);
          this.init(false, direction);
        });
      },
      error: () => {
        this.bookInfo = currentBookInfo;
        this.focusViewerOnLoad = false;
        this.isLoading.set(false);
      }
    });
  }

  private handleNoNextChapter(bookInfo: BookInfo) {
    if (!this.isLastPageEndInView()) {
      this.toastr.warning(translate('toasts.no-next-chapter', {entity: this.chapterEntityName()}));
      return;
    }

    if (this.readingListMode) {
      this.closeReader();
      return;
    }

    this.promptRateAndReview(bookInfo).subscribe(() => {
      this.router.navigate(['library', this.libraryId, 'series', this.seriesId]).catch(console.error);
    });
  }

  private chapterEntityName() {
    return this.entityTitleService.formatChapterName(this.libraryType).toLowerCase();
  }

  // Not currentPage: the viewer reports the first fully visible page, so with 278-282 on screen it stays on 279
  private isLastPageEndInView() {
    const lastPage = this.document.querySelector(`.page[data-page-number="${this.maxPages()}"]`);
    const viewer = this.document.querySelector('#viewerContainer');
    if (!lastPage || !viewer) return false;

    // Page mode keeps only the current page or spread in the DOM, so the element existing means it is showing
    if (this.scrollMode() === ScrollModeType.page) return true;

    const page = this.getPageContentRect(lastPage);
    const view = viewer.getBoundingClientRect();

    // 1px: fractional layout leaves the edge up to a pixel past the viewer at full scroll
    return this.scrollMode() === ScrollModeType.horizontal
      ? page.right <= view.right + 1
      : page.bottom <= view.bottom + 1;
  }

  private isFirstPageStartInView() {
    const firstPage = this.document.querySelector('.page[data-page-number="1"]');
    const viewer = this.document.querySelector('#viewerContainer');
    if (!firstPage || !viewer) return false;

    if (this.scrollMode() === ScrollModeType.page) return true;

    const page = this.getPageContentRect(firstPage);
    const view = viewer.getBoundingClientRect();

    return this.scrollMode() === ScrollModeType.horizontal
      ? page.left >= view.left - 1
      : page.top >= view.top - 1;
  }

  // pdf.js draws each page inside a transparent border (about 9px) and scrolls a page to the edge inside it
  private getPageContentRect(page: Element) {
    const rect = page.getBoundingClientRect();
    const border = parseFloat(getComputedStyle(page).borderTopWidth) || 0;
    return {top: rect.top + border, bottom: rect.bottom - border, left: rect.left + border, right: rect.right - border};
  }

  closeReader() {
     if (this.readerService.closeShortCutModal()) return;
     this.readerService.closeReader(this.libraryId, this.seriesId, this.chapterId(), this.readingListMode, this.readingListId);
  }

  openShortcutModal() {
     this.readerService.openShortcutModal(KEYBIND_TARGETS);
  }

  updateLoading(state: boolean) {
    this.isLoading.set(state);
  }

  updateLoadProgress(event: ProgressBarEvent) {
    this.loadPercent.set(event.percent);
  }


  updateSearchOpen(event: boolean) {
     this.isSearchOpen.set(event);
  }

  prevPage() {
    if (this.isFirstPageStartInView()) {
      this.loadPrevChapter();
      return;
    }

    if (this.getPdfViewer()?.previousPage()) return;

    // pdf.js is already on page 1, but its start is cut off
    this.scrollViewerOneScreen(-1);
  }

  nextPage() {
    if (this.isLastPageEndInView()) {
      this.loadNextChapter();
      return;
    }

    if (this.getPdfViewer()?.nextPage()) return;

    this.scrollViewerOneScreen(1);
  }

  private getPdfViewer() {
    return this.pdfNotificationService.onPDFJSInitSignal()?.pdfViewer as PdfJsViewer | undefined;
  }

  private scrollViewerOneScreen(direction: 1 | -1) {
    const viewer = this.document.querySelector('#viewerContainer');
    if (!viewer) return;

    if (this.scrollMode() === ScrollModeType.horizontal) {
      viewer.scrollBy({left: direction * viewer.clientWidth});
      return;
    }

    viewer.scrollBy({top: direction * viewer.clientHeight});
  }


  protected readonly Breakpoint = Breakpoint;
  protected readonly ScrollModeType = ScrollModeType;
}
