import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ElementRef,
  HostListener,
  inject, OnDestroy,
  OnInit, ViewChild
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgxExtendedPdfViewerService, PageViewModeType, ProgressBarEvent, NgxExtendedPdfViewerModule } from 'ngx-extended-pdf-viewer';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { BookService } from 'src/app/book-reader/_services/book.service';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { CHAPTER_ID_DOESNT_EXIST, ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { ThemeService } from 'src/app/_services/theme.service';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { NgIf, NgStyle, AsyncPipe } from '@angular/common';
import {translate, TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-pdf-reader',
    templateUrl: './pdf-reader.component.html',
    styleUrls: ['./pdf-reader.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgStyle, NgxExtendedPdfViewerModule, NgbTooltip, AsyncPipe, TranslocoDirective]
})
export class PdfReaderComponent implements OnInit, OnDestroy {

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

  /**
   * This can't be updated dynamically:
   * https://github.com/stephanrauh/ngx-extended-pdf-viewer/issues/1415
   */
  bookMode: PageViewModeType = 'multiple';

  constructor(private route: ActivatedRoute, private router: Router, public accountService: AccountService,
    private seriesService: SeriesService, public readerService: ReaderService,
    private navService: NavService, private toastr: ToastrService,
    private bookService: BookService, private themeService: ThemeService,
    private readonly cdRef: ChangeDetectorRef, private pdfViewerService: NgxExtendedPdfViewerService) {
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

  init() {
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
    this.readerService.enableWakeLock(this.container.nativeElement);
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

  toggleBookPageMode() {
    if (this.bookMode === 'book') {
      this.bookMode = 'multiple';
    } else {
      this.bookMode = 'book';
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

}
