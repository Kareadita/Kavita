import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  HostListener,
  inject,
  Inject,
  OnDestroy,
  OnInit,
  ViewChild
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {NgxExtendedPdfViewerModule, PageViewModeType, ProgressBarEvent, ScrollModeType} from 'ngx-extended-pdf-viewer';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs';
import {BookService} from 'src/app/book-reader/_services/book.service';
import {Breakpoint, KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter} from 'src/app/_models/chapter';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {NavService} from 'src/app/_services/nav.service';
import {CHAPTER_ID_DOESNT_EXIST, ReaderService} from 'src/app/_services/reader.service';
import {SeriesService} from 'src/app/_services/series.service';
import {ThemeService} from 'src/app/_services/theme.service';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {AsyncPipe, DOCUMENT, NgStyle} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {PdfLayoutMode} from "../../../_models/preferences/pdf-layout-mode";
import {PdfScrollMode} from "../../../_models/preferences/pdf-scroll-mode";
import {PdfTheme} from "../../../_models/preferences/pdf-theme";
import {PdfSpreadMode} from "../../../_models/preferences/pdf-spread-mode";
import {SpreadType} from "ngx-extended-pdf-viewer/lib/options/spread-type";
import {PdfLayoutModePipe} from "../../_pipe/pdf-layout-mode.pipe";
import {PdfScrollModeTypePipe} from "../../_pipe/pdf-scroll-mode.pipe";
import {PdfSpreadTypePipe} from "../../_pipe/pdf-spread-mode.pipe";

@Component({
    selector: 'app-pdf-reader',
    templateUrl: './pdf-reader.component.html',
    styleUrls: ['./pdf-reader.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgStyle, NgxExtendedPdfViewerModule, NgbTooltip, AsyncPipe, TranslocoDirective,
    PdfLayoutModePipe, PdfScrollModeTypePipe, PdfSpreadTypePipe]
})
export class PdfReaderComponent implements OnInit, OnDestroy {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly navService = inject(NavService);
  private readonly toastr = inject(ToastrService);
  private readonly bookService = inject(BookService);
  private readonly themeService = inject(ThemeService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly accountService = inject(AccountService);
  public readonly readerService = inject(ReaderService);
  public readonly utilityService = inject(UtilityService);
  public readonly destroyRef = inject(DestroyRef);

  protected readonly ScrollModeType = ScrollModeType;
  protected readonly Breakpoint = Breakpoint;

  @ViewChild('container') container!: ElementRef;

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
   * Current Page number
   */
  currentPage: number = 1;
  /**
   * Total pages
   */
  maxPages: number = 1;
  bookTitle: string = '';

  zoomSetting: string | number = 'auto';

  theme: 'dark' | 'light' = 'light';
  themeMap: {[key:string]: {background: string, font: string}} = {
    'dark': {'background': '#292929', 'font': '#d9d9d9'},
    'light': {'background': '#f9f9f9', 'font': '#5a5a5a'}
  }
  backgroundColor: string = this.themeMap[this.theme].background;
  fontColor: string = this.themeMap[this.theme].font;

  isLoading: boolean = true;
  /**
   * How much of the current document is loaded
   */
  loadPercent: number = 0;
  scrollbarNeeded = false;

  pageLayoutMode: PageViewModeType = 'multiple';
  scrollMode: ScrollModeType = ScrollModeType.vertical;
  spreadMode: SpreadType = 'off';

  constructor(@Inject(DOCUMENT) private document: Document) {
      this.navService.hideNavBar();
      this.themeService.clearThemes();
      this.navService.hideSideNav();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.ESC_KEY) {
      this.closeReader();
    }
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize(){
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

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.init();
      }
    });
  }


  calcScrollbarNeeded() {
    const viewContainer = this.document.querySelector('#viewerContainer');
    if (viewContainer == null) return;
    this.scrollbarNeeded = viewContainer.scrollHeight > this.container?.nativeElement?.clientHeight;
    this.cdRef.markForCheck();
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

  init() {

    this.pageLayoutMode = this.convertPdfLayoutMode(PdfLayoutMode.Multiple);
    this.scrollMode = this.convertPdfScrollMode(this.user.preferences.pdfScrollMode || PdfScrollMode.Vertical);
    this.spreadMode = this.convertPdfSpreadMode(this.user.preferences.pdfSpreadMode || PdfSpreadMode.None);
    this.theme = this.convertPdfTheme(this.user.preferences.pdfTheme || PdfTheme.Dark);
    this.backgroundColor = this.themeMap[this.theme].background;
    this.fontColor = this.themeMap[this.theme].font; // TODO: Move this to an observable or something

    this.calcScrollbarNeeded();

    this.bookService.getBookInfo(this.chapterId).subscribe(info => {
      this.volumeId = info.volumeId;
      this.bookTitle = info.bookTitle;
      this.cdRef.markForCheck();
    });

    this.readerService.getProgress(this.chapterId).subscribe(progress => {
      this.currentPage = progress.pageNum || 1;
      this.cdRef.markForCheck();
    });

    this.seriesService.getChapter(this.chapterId).subscribe(chapter => {
      this.maxPages = chapter.pages;

      if (this.currentPage >= this.maxPages) {
        this.currentPage = this.maxPages - 1;
        this.saveProgress();
      }
      this.cdRef.markForCheck();
    });
    setTimeout(() => this.readerService.enableWakeLock(this.container.nativeElement), 1000);
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
   turnOffIncognito() {
    this.incognitoMode = false;
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info(translate('toasts.incognito-off'));
    this.saveProgress();
    this.cdRef.markForCheck();
  }

  toggleTheme() {
    if (this.theme === 'dark') {
      this.theme = 'light';
    } else {
      this.theme = 'dark';
    }
    this.backgroundColor = this.themeMap[this.theme].background;
    this.fontColor = this.themeMap[this.theme].font;
    this.cdRef.markForCheck();
  }

  toggleScrollMode() {
    const options: Array<ScrollModeType> = [ScrollModeType.vertical, ScrollModeType.horizontal, ScrollModeType.page];
    let index = options.indexOf(this.scrollMode) + 1;
    if (index >= options.length) index = 0;
    this.scrollMode = options[index];

    this.calcScrollbarNeeded();
    const currPage = this.currentPage;
    this.cdRef.markForCheck();

    setTimeout(() => {
      this.currentPage = currPage;
      this.cdRef.markForCheck();
    }, 100);
  }

  toggleSpreadMode() {
     const options: Array<SpreadType> = ['off', 'odd', 'even'];
     let index = options.indexOf(this.spreadMode) + 1;
     if (index >= options.length) index = 0;
     this.spreadMode = options[index];


    this.cdRef.markForCheck();
  }

  toggleBookPageMode() {
    if (this.pageLayoutMode === 'book') {
      this.pageLayoutMode = 'multiple';
    } else {
      if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
        this.toastr.info(translate('toasts.pdf-book-mode-screen-size'));
        return;
      }
      this.pageLayoutMode = 'book';
      // If the fit is automatic, let's adjust to 100% to ensure it renders correctly (can't do this, but it doesn't always happen)
    }
    this.cdRef.markForCheck();
  }

  saveProgress() {
    if (this.incognitoMode) return;
    this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, this.chapterId, this.currentPage).subscribe();
  }

  closeReader() {
    this.readerService.closeReader(this.readingListMode, this.readingListId);
  }

  updateLoading(state: boolean) {
    this.isLoading = state;
    this.cdRef.markForCheck();
  }

  updateLoadProgress(event: ProgressBarEvent) {
    this.loadPercent = event.percent;
    this.cdRef.markForCheck();
  }

  updateHandTool(event: any) {
     console.log('event.tool', event);
  }

  prevPage() {
     this.currentPage--;
     if (this.currentPage < 0) this.currentPage = 0;
     this.cdRef.markForCheck();
  }

  nextPage() {
    this.currentPage++;
    if (this.currentPage > this.maxPages) this.currentPage = this.maxPages;
    this.cdRef.markForCheck();
  }

}
