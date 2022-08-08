import { Location } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PageViewModeType } from 'ngx-extended-pdf-viewer';
import { ToastrService } from 'ngx-toastr';
import { Subject, take } from 'rxjs';
import { BookService } from 'src/app/book-reader/book.service';
import { Chapter } from 'src/app/_models/chapter';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';
import { NavService } from 'src/app/_services/nav.service';
import { CHAPTER_ID_DOESNT_EXIST, ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { ThemeService } from 'src/app/_services/theme.service';

@Component({
  selector: 'app-pdf-reader',
  templateUrl: './pdf-reader.component.html',
  styleUrls: ['./pdf-reader.component.scss']
})
export class PdfReaderComponent implements OnInit, OnDestroy {

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

  isLoading: boolean = false;

  /**
   * This can't be updated dynamically: 
   * https://github.com/stephanrauh/ngx-extended-pdf-viewer/issues/1415
   */
  bookMode: PageViewModeType = 'multiple';

  private readonly onDestroy = new Subject<void>();

  constructor(private route: ActivatedRoute, private router: Router, public accountService: AccountService,
    private seriesService: SeriesService, public readerService: ReaderService,
    private navService: NavService, private toastr: ToastrService,
    private bookService: BookService, private themeService: ThemeService, private location: Location) {
      this.navService.hideNavBar();
      this.themeService.clearThemes();
      this.navService.hideSideNav();
  }

  ngOnDestroy(): void {
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
    });

    this.readerService.getProgress(this.chapterId).subscribe(progress => {
      this.currentPage = progress.pageNum || 1;
    });

    this.seriesService.getChapter(this.chapterId).subscribe(chapter => {
      this.maxPages = chapter.pages;

      if (this.currentPage >= this.maxPages) {
        this.currentPage = this.maxPages - 1;
        this.saveProgress();
      }
    });

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

  toggleTheme() {
    if (this.theme === 'dark') {
      this.theme = 'light';
    } else {
      this.theme = 'dark';
    }
    this.backgroundColor = this.themeMap[this.theme].background;
    this.fontColor = this.themeMap[this.theme].font;
  }

  toggleBookPageMode() {
    if (this.bookMode === 'book') {
      this.bookMode = 'multiple';
    } else {
      this.bookMode = 'book';
    }
  }

  saveProgress() {
    if (this.incognitoMode) return;
    this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, this.currentPage).subscribe(() => {});
  }

  closeReader() {
    if (this.readingListMode) {
      this.router.navigateByUrl('lists/' + this.readingListId);
    } else {
      this.location.back();
    }
  }

}
