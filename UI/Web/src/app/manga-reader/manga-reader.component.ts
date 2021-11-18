import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, OnDestroy, OnInit, Renderer2, SimpleChanges, ViewChild } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { take, takeUntil } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ReaderService } from '../_services/reader.service';
import { FormBuilder, FormGroup } from '@angular/forms';
import { NavService } from '../_services/nav.service';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { PageSplitOption } from '../_models/preferences/page-split-option';
import { forkJoin, ReplaySubject, Subject } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { KEY_CODES, UtilityService, Breakpoint } from '../shared/_services/utility.service';
import { CircularArray } from '../shared/data-structures/circular-array';
import { MemberService } from '../_services/member.service';
import { Stack } from '../shared/data-structures/stack';
import { ChangeContext, LabelType, Options } from '@angular-slider/ngx-slider';
import { trigger, state, style, transition, animate } from '@angular/animations';
import { ChapterInfo } from './_models/chapter-info';
import { COLOR_FILTER, FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART } from './_models/reader-enums';
import { pageSplitOptions, scalingOptions } from '../_models/preferences/preferences';
import { READER_MODE } from '../_models/preferences/reader-mode';
import { MangaFormat } from '../_models/manga-format';
import { LibraryService } from '../_services/library.service';
import { LibraryType } from '../_models/library';

const PREFETCH_PAGES = 5;

const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

const ANIMATION_SPEED = 200;
const OVERLAY_AUTO_CLOSE_TIME = 6000;
const CLICK_OVERLAY_TIMEOUT = 3000;


@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss'],
  animations: [
    trigger('slideFromTop', [
      state('in', style({ transform: 'translateY(0)'})),
      transition('void => *', [
        style({ transform: 'translateY(-100%)' }),
        animate(ANIMATION_SPEED)
      ]),
      transition('* => void', [
        animate(ANIMATION_SPEED, style({ transform: 'translateY(-100%)' })),
      ])
    ]),
    trigger('slideFromBottom', [
      state('in', style({ transform: 'translateY(0)'})),
      transition('void => *', [
        style({ transform: 'translateY(100%)' }),
        animate(ANIMATION_SPEED)
      ]),
      transition('* => void', [
        animate(ANIMATION_SPEED, style({ transform: 'translateY(100%)' })),
      ])
    ])
  ]
})
export class MangaReaderComponent implements OnInit, AfterViewInit, OnDestroy {
  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
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
   * The current page. UI will show this number + 1.
   */
  pageNum = 0;
  /**
   * Total pages in the given Chapter
   */
  maxPages = 1;
  user!: User;
  generalSettingsForm!: FormGroup;

  scalingOptions = scalingOptions;
  readingDirection = ReadingDirection.LeftToRight;
  scalingOption = ScalingOption.FitToHeight;
  pageSplitOption = PageSplitOption.FitSplit;
  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;
  colorMode: COLOR_FILTER = COLOR_FILTER.NONE;
  autoCloseMenu: boolean = true;
  readerMode: READER_MODE = READER_MODE.MANGA_LR;

  pageSplitOptions = pageSplitOptions;
  
  isLoading = true; 

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private canvasImage = new Image();

  /**
   * A circular array of size PREFETCH_PAGES + 2. Maintains prefetched Images around the current page to load from to avoid loading animation.
   * @see CircularArray
   */
  cachedImages!: CircularArray<HTMLImageElement>;
  /**
   * A stack of the chapter ids we come across during continuous reading mode. When we traverse a boundary, we use this to avoid extra API calls.
   * @see Stack
   */
  continuousChaptersStack: Stack<number> = new Stack();

  /**
   * An event emiter when a page change occurs. Used soley by the webtoon reader.
   */
   goToPageEvent: ReplaySubject<number> = new ReplaySubject<number>();
   /**
   * An event emiter when a bookmark on a page change occurs. Used soley by the webtoon reader.
   */
   showBookmarkEffectEvent: ReplaySubject<number> = new ReplaySubject<number>();
  /**
   * If the menu is open/visible.
   */
  menuOpen = false;
  /**
   * If the prev page allows a page change to occur.
   */
  prevPageDisabled = false;
  /**
   * If the next page allows a page change to occur.
   */
  nextPageDisabled = false;
  pageOptions: Options = {
    floor: 0,
    ceil: 0,
    step: 1,
    boundPointerLabels: true,
    showSelectionBar: true,
    translate: (value: number, label: LabelType) => {
      if (label == LabelType.Floor) {
        return 1 + '';
      } else if (label === LabelType.Ceil) {
        return this.maxPages + '';
      }
      return (this.pageNum + 1) + '';
    },
    animate: false
  };
  refreshSlider: EventEmitter<void> = new EventEmitter<void>();

  /**
   * Used to store the Series name for UI
   */
  title: string = '';
  /**
   * Used to store the Volume/Chapter information
   */
  subtitle: string = '';
  /**
   * Timeout id for auto-closing menu overlay
   */
  menuTimeout: any;
  /**
   * If the click overlay is rendered on screen
   */
  showClickOverlay: boolean = false;
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
   * If extended settings area is visible. Blocks auto-closing of menu.
   */
  settingsOpen: boolean = false;
  /**
   * A map of bookmarked pages to anything. Used for O(1) lookup time if a page is bookmarked or not.
   */
  bookmarks: {[key: string]: number} = {};
  /**
   * Tracks if the first page is rendered or not. This is used to keep track of Automatic Scaling and adjusting decision after first page dimensions load up.
   */
  firstPageRendered: boolean = false;
  /**
   * Library Type used for rendering chapter or issue
   */
  libraryType: LibraryType = LibraryType.Manga;
  /**
   * Max width for images that we've seen. Will automatically cut cover images in 2 for calculations.
   */
  maxWidth: number = 0;

  private readonly onDestroy = new Subject<void>();

  
  getPageUrl = (pageNum: number) => this.readerService.getPageUrl(this.chapterId, pageNum);

  

  get pageBookmarked() {
    return this.bookmarks.hasOwnProperty(this.pageNum);
  }
  

  get splitIconClass() {
    if (this.isSplitLeftToRight()) {
      return 'left-side';
    } else if (this.isNoSplit()) {
      return 'none';  
    }
    return 'right-side';
  }

  get readerModeIcon() {
    switch(this.readerMode) {
      case READER_MODE.MANGA_LR:
        return 'fa-exchange-alt';
      case READER_MODE.MANGA_UD:
        return 'fa-exchange-alt fa-rotate-90';
      case READER_MODE.WEBTOON:
        return 'fa-arrows-alt-v';
    }
  }

  get colorOptionName() {
    switch(this.colorMode) {
      case COLOR_FILTER.NONE:
        return 'None';
      case COLOR_FILTER.DARK:
        return 'Dark';
      case COLOR_FILTER.SEPIA:
        return 'Sepia';
    }
  }

  get READER_MODE(): typeof READER_MODE {
    return READER_MODE;
  }

  get ReadingDirection(): typeof ReadingDirection {
    return ReadingDirection;
  }

  get PageSplitOption(): typeof PageSplitOption {
    return PageSplitOption;
  }

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
              public readerService: ReaderService, private location: Location,
              private formBuilder: FormBuilder, private navService: NavService, 
              private toastr: ToastrService, private memberService: MemberService,
              private libraryService: LibraryService, private utilityService: UtilityService, 
              private renderer: Renderer2) {
                this.navService.hideNavBar();
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
    

    this.continuousChaptersStack.push(this.chapterId);

    this.readerService.setOverrideStyles();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.readingDirection = this.user.preferences.readingDirection;
        this.scalingOption = this.user.preferences.scalingOption;
        this.pageSplitOption = this.user.preferences.pageSplitOption;
        this.autoCloseMenu = this.user.preferences.autoCloseMenu;
        this.readerMode = this.user.preferences.readerMode;


        this.generalSettingsForm = this.formBuilder.group({
          autoCloseMenu: this.autoCloseMenu,
          pageSplitOption: this.pageSplitOption,
          fittingOption: this.translateScalingOption(this.scalingOption)
        });

        this.updateForm();

        this.generalSettingsForm.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((changes: SimpleChanges) => {
          this.autoCloseMenu = this.generalSettingsForm.get('autoCloseMenu')?.value;
          const needsSplitting = this.isCoverImage();
          // If we need to split on a menu change, then we need to re-render. 
          if (needsSplitting) {
            this.loadPage();
          }
        });

        this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(progress => {
          if (!progress) {
            this.toggleMenu();
            this.toastr.info('Tap the image at any time to open the menu. You can configure different settings or go to page by clicking progress bar. Tap sides of image move to next/prev page.');
          }
        });
      } else {
        // If no user, we can't render 
        this.router.navigateByUrl('/login');
      }
    });


    this.init();
  }

  ngAfterViewInit() {
    if (!this.canvas) {
      return;
    }
    this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });
    this.canvasImage.onload = () => this.renderPage();
  }

  ngOnDestroy() {
    this.readerService.resetOverrideStyles();
    this.navService.showNavBar();
    this.onDestroy.next();
    this.onDestroy.complete();
    this.goToPageEvent.complete();
    this.showBookmarkEffectEvent.complete();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {

    switch (this.readerMode) {
      case READER_MODE.MANGA_LR:
        if (event.key === KEY_CODES.RIGHT_ARROW) {
          this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
        } else if (event.key === KEY_CODES.LEFT_ARROW) {
          this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
        }
        break;
      case READER_MODE.MANGA_UD:
      case READER_MODE.WEBTOON:
        if (event.key === KEY_CODES.DOWN_ARROW) {
          this.nextPage()
        } else if (event.key === KEY_CODES.UP_ARROW) {
          this.prevPage()
        }
        break;
    }

    if (event.key === KEY_CODES.ESC_KEY) {
      if (this.menuOpen) {
        this.toggleMenu();
        event.stopPropagation();
        event.preventDefault();
        return;
      }
      this.closeReader();
    } else if (event.key === KEY_CODES.SPACE) {
      this.toggleMenu();
    } else if (event.key === KEY_CODES.G) {
      const goToPageNum = this.promptForPage();
      if (goToPageNum === null) { return; }
      this.goToPage(parseInt(goToPageNum.trim(), 10));
    } else if (event.key === KEY_CODES.B) {
      this.bookmarkPage();
    }
  }

  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.pageNum = 0;

    forkJoin({
      progress: this.readerService.getProgress(this.chapterId),
      chapterInfo: this.readerService.getChapterInfo(this.chapterId),
      bookmarks: this.readerService.getBookmarks(this.chapterId),
    }).pipe(take(1)).subscribe(results => {

      if (this.readingListMode && results.chapterInfo.seriesFormat === MangaFormat.EPUB) {
        // Redirect to the book reader. 
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(['library', results.chapterInfo.libraryId, 'series', results.chapterInfo.seriesId, 'book', this.chapterId], {queryParams: params});
        return;
      }

      this.volumeId = results.chapterInfo.volumeId;
      this.maxPages = results.chapterInfo.pages;
      let page = results.progress.pageNum;
      if (page > this.maxPages) {
        page = this.maxPages;
      }
      this.setPageNum(page);
      
      

      // Due to change detection rules in Angular, we need to re-create the options object to apply the change
      const newOptions: Options = Object.assign({}, this.pageOptions);
      newOptions.ceil = this.maxPages - 1; // We -1 so that the slider UI shows us hitting the end, since visually we +1 everything.
      this.pageOptions = newOptions;

      this.libraryService.getLibraryType(results.chapterInfo.libraryId).pipe(take(1)).subscribe(type => {
        this.libraryType = type;
        this.updateTitle(results.chapterInfo, type);
      });

      

      // From bookmarks, create map of pages to make lookup time O(1)
      this.bookmarks = {};
      results.bookmarks.forEach(bookmark => {
        this.bookmarks[bookmark.page] = 1;
      });

      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
          this.nextChapterDisabled = true;
        }
      });
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
          this.prevChapterDisabled = true;
        }
      });


      const images = [];
      for (let i = 0; i < PREFETCH_PAGES + 2; i++) {
        images.push(new Image());
      }

      this.cachedImages = new CircularArray<HTMLImageElement>(images, 0);


      this.render();
    }, () => {
      setTimeout(() => {
        this.closeReader();
      }, 200);
    });
  }

  render() {
    if (this.readerMode === READER_MODE.WEBTOON) {
      this.isLoading = false;
    } else {
      this.loadPage();
    }
  }

  closeReader() {
    if (this.readingListMode) {
      this.router.navigateByUrl('lists/' + this.readingListId);
    } else {
      this.location.back();
    }
  }

  updateTitle(chapterInfo: ChapterInfo, type: LibraryType) {
      this.title = chapterInfo.seriesName;
      if (chapterInfo.chapterTitle.length > 0) {
        this.title += ' - ' + chapterInfo.chapterTitle;
      }

      this.subtitle = '';
      if (chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
        this.subtitle = chapterInfo.fileName;
      } else if (!chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
        this.subtitle = this.utilityService.formatChapterName(type, true, true) + chapterInfo.chapterNumber;
      } else {
        this.subtitle = 'Volume ' + chapterInfo.volumeNumber;

        if (chapterInfo.chapterNumber !== '0') {
          this.subtitle += ' ' + this.utilityService.formatChapterName(type, true, true) + chapterInfo.chapterNumber;
        }
      }
  }

  translateScalingOption(option: ScalingOption) {
    switch (option) {
      case (ScalingOption.Automatic):
      {
        const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
        const windowHeight = window.innerHeight
                  || document.documentElement.clientHeight
                  || document.body.clientHeight;

        const ratio = windowWidth / windowHeight;
        if (windowHeight > windowWidth) {
          return FITTING_OPTION.WIDTH;
        }

        if (windowWidth >= windowHeight || ratio > 1.0) {
          return FITTING_OPTION.HEIGHT;
        }
        return FITTING_OPTION.WIDTH;
      }
      case (ScalingOption.FitToHeight):
        return FITTING_OPTION.HEIGHT;
      case (ScalingOption.FitToWidth):
        return FITTING_OPTION.WIDTH;
      default:
        return FITTING_OPTION.ORIGINAL;
    }
  }

  getFittingOptionClass() {
    const formControl = this.generalSettingsForm.get('fittingOption');
    if (formControl === undefined) {
      return FITTING_OPTION.HEIGHT;
    }
    return formControl?.value;
  }

  getFittingIcon() {
    const value = this.getFit();
    
    switch(value) {
      case FITTING_OPTION.HEIGHT:
        return 'fa-arrows-alt-v';
      case FITTING_OPTION.WIDTH:
        return 'fa-arrows-alt-h';
      case FITTING_OPTION.ORIGINAL:
        return 'fa-expand-arrows-alt';
    }
  }

  getFit() {
    let value = FITTING_OPTION.HEIGHT;
    const formControl = this.generalSettingsForm.get('fittingOption');
    if (formControl !== undefined) {
      value = formControl?.value;
    }
    return value;
  }

  cancelMenuCloseTimer() {
    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
    }
  }

  /**
   * Whenever the menu is interacted with, restart the timer. However if the settings menu is open, don't restart, just cancel the timeout.
   */
  resetMenuCloseTimer() {
    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
      if (!this.settingsOpen && this.autoCloseMenu) {
        this.startMenuCloseTimer();
      }
    }
  }

  startMenuCloseTimer() {
    if (!this.autoCloseMenu) { return; }

    this.menuTimeout = setTimeout(() => {
      this.toggleMenu();
    }, OVERLAY_AUTO_CLOSE_TIME);
  }

 
  toggleMenu() {
    this.menuOpen = !this.menuOpen;
    
    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
    }

    if (this.menuOpen && !this.settingsOpen) {
      this.startMenuCloseTimer();
    } else {
      this.showClickOverlay = false;
      this.settingsOpen = false;
    }
  }

  isSplitLeftToRight() {
    return parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10) === PageSplitOption.SplitLeftToRight;
  }

  /**
   * 
   * @returns If the current model reflects no split of fit split
   */
  isNoSplit() {
    const splitValue = parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10);
    return  splitValue === PageSplitOption.NoSplit || splitValue === PageSplitOption.FitSplit;
  }

  updateSplitPage() {
    const needsSplitting = this.isCoverImage();
    if (!needsSplitting || this.isNoSplit()) {
      this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
      return;
    }

    if (this.pagingDirection === PAGING_DIRECTION.FORWARD) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const r2lSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : r2lSplittingPart;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight() ? l2rSplittingPart : SPLIT_PAGE_PART.LEFT_PART;
          break;
      }
    } else if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = this.isSplitLeftToRight() ? l2rSplittingPart : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          break;
      }
    }
  }

  handlePageChange(event: any, direction: string) {
    if (this.readerMode === READER_MODE.WEBTOON) {
      if (direction === 'right') {
        this.nextPage(event);
      } else {
        this.prevPage(event);
      }
      return;
    }
    if (direction === 'right') {
      this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage(event) : this.prevPage(event);
    } else if (direction === 'left') {
      this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage(event) : this.nextPage(event);
    }
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART);

    if ((this.pageNum + 1 >= this.maxPages && notInSplit) || this.isLoading) {

      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum + 1);
      if (this.readerMode !== READER_MODE.WEBTOON) {
        this.canvasImage = this.cachedImages.next();
      }
    }

    if (this.readerMode !== READER_MODE.WEBTOON) {
      this.loadPage();
    }    
  }

  prevPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART);

    if ((this.pageNum - 1 < 0 && notInSplit) || this.isLoading) {

      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum - 1);
      this.canvasImage = this.cachedImages.prev();
    }

    if (this.readerMode !== READER_MODE.WEBTOON) {
      this.loadPage();
    }  
  }

  loadNextChapter() {
    if (this.nextPageDisabled) { return; }
    if (this.nextChapterDisabled) { return; }
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
    if (this.prevChapterDisabled) { return; }
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

    if (this.prevChapterId === CHAPTER_ID_NOT_FETCHED || this.prevChapterId === this.chapterId) {
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

  /**
   * There are some hard limits on the size of canvas' that we must cap at. https://github.com/jhildenbiddle/canvas-size#test-results
   * For Safari, it's 16,777,216, so we cap at 4096x4096 when this happens. The drawImage in render will perform bi-cubic scaling for us.
   * @returns If we should continue to the render loop 
   */
  setCanvasSize() {
    if (this.ctx && this.canvas) {
      // eslint-disable-next-line @typescript-eslint/ban-ts-comment
      // @ts-ignore
      const isSafari = [
        'iPad Simulator',
        'iPhone Simulator',
        'iPod Simulator',
        'iPad',
        'iPhone',
        'iPod'
      ].includes(navigator.platform)
      // iPad on iOS 13 detection
      || (navigator.userAgent.includes("Mac") && "ontouchend" in document);
      const canvasLimit = isSafari ? 16_777_216 : 124_992_400;
      const needsScaling = this.canvasImage.width * this.canvasImage.height > canvasLimit;
      if (needsScaling) {
        this.canvas.nativeElement.width = isSafari ? 4_096 : 16_384;
        this.canvas.nativeElement.height = isSafari ? 4_096 : 16_384;
      } else if (this.isCoverImage()) {
        //this.canvas.nativeElement.width = this.canvasImage.width / 2;
        //this.canvas.nativeElement.height = this.canvasImage.height;
      } else {
        this.canvas.nativeElement.width = this.canvasImage.width;
        this.canvas.nativeElement.height = this.canvasImage.height;
      }
    }
  }

  renderPage() {
    console.log('Render page called');
    if (this.ctx && this.canvas) {
      this.canvasImage.onload = null;

      this.setCanvasSize();

      const needsSplitting = this.isCoverImage();
      this.updateSplitPage();

      if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
        this.canvas.nativeElement.width = this.canvasImage.width / 2;
        this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
      } else if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
        this.canvas.nativeElement.width = this.canvasImage.width / 2;
        this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
      } else {
        if (!this.firstPageRendered && this.scalingOption === ScalingOption.Automatic) {
          this.updateScalingForFirstPageRender();
        }

        this.maxWidth = Math.max((this.isCoverImage() ? (this.canvasImage.width / 2) : this.canvasImage.width), this.maxWidth);
        const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
          const windowHeight = window.innerHeight
                  || document.documentElement.clientHeight
                  || document.body.clientHeight;
        

        // Fit Split on a page that needs splitting
        if (this.shouldRenderAsFitSplit()) {
          console.log('Adjusting settings to render as fit split');
          // If the user's screen is wider than the image, just pretend this is no split, as it will render nicer
          
          //console.log('window width: ', windowWidth);
          //console.log('image width: ', this.canvasImage.width);

          // ?! There is an issue here where on a tablet, the windowWidth is not correct on first pass here
          //this.generalSettingsForm.get('fittingOption')?.setValue(FITTING_OPTION.WIDTH, {emitEvent: false});
          this.canvas.nativeElement.width = windowWidth;
          this.canvas.nativeElement.height = windowHeight;
          const ratio = this.canvasImage.width / this.canvasImage.height;
          let newWidth = windowWidth; // this.canvas.nativeElement.width
          let newHeight = newWidth / ratio;
          if (newHeight > windowHeight) { //this.canvas.nativeElement.height
            newHeight = windowHeight; // this.canvas.nativeElement.height
            newWidth = newHeight * ratio;
          }
          console.log('new width x height: ', newWidth, 'x', newHeight);
          this.ctx.drawImage(this.canvasImage, 0, 0, newWidth, newHeight);
          //this.ctx.drawImage(this.canvasImage, 0, 0);
        } else {
          this.ctx.drawImage(this.canvasImage, 0, 0);
        }
      }
      
      // Reset scroll on non HEIGHT Fits
      if (this.getFit() !== FITTING_OPTION.HEIGHT) {
        window.scrollTo(0, 0);
      }

    }
    this.isLoading = false;
  }

  updateScalingForFirstPageRender() {
    console.log('Updating scale from automatic for first page render');
    const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
    const windowHeight = window.innerHeight
              || document.documentElement.clientHeight
              || document.body.clientHeight;
      
      const needsSplitting = this.isCoverImage();
      let newScale = this.generalSettingsForm.get('fittingOption')?.value;
      const widthRatio = windowWidth / (this.canvasImage.width / (needsSplitting ? 2 : 1));
      const heightRatio = windowHeight / (this.canvasImage.height);

      // Given that we now have image dimensions, assuming this isn't a split image, 
      // Try to reset one time based on who's dimension (width/height) is smaller
      if (widthRatio < heightRatio) {
        newScale = FITTING_OPTION.WIDTH;
      } else if (widthRatio > heightRatio) {
        newScale = FITTING_OPTION.HEIGHT;
      }

      this.firstPageRendered = true;
      console.log('Scale: ', newScale);
      
      this.generalSettingsForm.get('fittingOption')?.setValue(newScale, {emitEvent: false});
  }

  isCoverImage() {
    return this.canvasImage.width > this.canvasImage.height;
  }

  // ?! There is an issue where first load with Automatic scaling, will cause this to not render correctly (cuttof)
  // ?! When the screen size is much longer than the image, then using NoSplit renders it much nicer (minor optimization)
  shouldRenderAsFitSplit() {
    // NOTE: I Think fit split rendering adjustments also need to take into account FIT as HEIGHT and Tablet, as those 2 cause breakages
    if (!this.isCoverImage() || parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10) !== PageSplitOption.FitSplit) return false;
    // took  || !this.firstPageRendered off

    return true;
    // const breakpoint = this.utilityService.getActiveBreakpoint();
    // //if (breakpoint === Breakpoint.Tablet && this.generalSettingsForm.get('fittingOption')?.value === FITTING_OPTION.HEIGHT) return false;

    
    // console.log('breakpoitn: ', breakpoint);
    // return (
    //           this.firstPageRendered
    //           && breakpoint !== Breakpoint.Mobile);
  }


  prefetch() {
    let index = 1;

    this.cachedImages.applyFor((item, internalIndex) => {
      const offsetIndex = this.pageNum + index;
      const urlPageNum = this.readerService.imageUrlToPageNum(item.src);
      if (urlPageNum === offsetIndex) {
        index += 1;
        return;
      }
      if (offsetIndex < this.maxPages - 1) {
        item.src = this.readerService.getPageUrl(this.chapterId, offsetIndex);
        index += 1;
      }
    }, this.cachedImages.size() - 3);
    //console.log('prefetched images: ', this.cachedImages.arr.map(item => this.readerService.imageUrlToPageNum(item.src) + (item.complete ? ' (c)' : '')));
  }

  loadPage() {
    if (!this.canvas || !this.ctx) { return; }

    this.isLoading = true;
    this.canvasImage = this.cachedImages.current();
    if (this.readerService.imageUrlToPageNum(this.canvasImage.src) !== this.pageNum || this.canvasImage.src === '' || !this.canvasImage.complete) {
      this.canvasImage.src = this.readerService.getPageUrl(this.chapterId, this.pageNum);
      this.canvasImage.onload = () => this.renderPage();
    } else {
      this.renderPage();
    }
    this.prefetch();
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }

    if (this.menuOpen) {
      this.showClickOverlay = true;
      setTimeout(() => {
        this.showClickOverlay = false;
      }, CLICK_OVERLAY_TIMEOUT);
    }
  }

  clickOverlayClass(side: 'right' | 'left') {
    if (!this.showClickOverlay) {
      return '';
    }

    if (this.readingDirection === ReadingDirection.LeftToRight) {
      return side === 'right' ? 'highlight' : 'highlight-2';
    }
    return side === 'right' ? 'highlight-2' : 'highlight';
  }

  sliderDragUpdate(context: ChangeContext) {
    // This will update the value for value except when in webtoon due to how the webtoon reader 
    // responds to page changes
    if (this.readerMode !== READER_MODE.WEBTOON) {
      this.setPageNum(context.value);
    }
  }

  sliderPageUpdate(context: ChangeContext) {
    const page = context.value;
    
    if (page > this.pageNum) {
      this.pagingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    }

    this.setPageNum(page);
    this.refreshSlider.emit();
    this.goToPageEvent.next(page);
    this.render();
  }

  setPageNum(pageNum: number) {
    this.pageNum = pageNum;

    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (this.nextChapterId > 0 && !this.nextChapterPrefetched) {
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1)).subscribe(res => {
          this.nextChapterPrefetched = true;
        });
      }
    } else if (this.pageNum <= 10) {
      if (this.prevChapterId > 0 && !this.prevChapterPrefetched) {
        this.readerService.getChapterInfo(this.prevChapterId).pipe(take(1)).subscribe(res => {
          this.prevChapterPrefetched = true;
        });
      }
    }

    // Due to the fact that we start at image 0, but page 1, we need the last page to have progress as page + 1 to be completed
    let tempPageNum = this.pageNum;
    if (this.pageNum == this.maxPages - 1) {
      tempPageNum = this.pageNum + 1;
    }

    if (!this.incognitoMode) {
      this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, tempPageNum).pipe(take(1)).subscribe(() => {/* No operation */});
    }
  }

  goToPage(pageNum: number) {
    let page = pageNum;
    
    if (page === undefined || this.pageNum === page) { return; }

    if (page > this.maxPages) {
      page = this.maxPages;
    } else if (page < 0) {
      page = 0;
    }

    if (!(page === 0 || page === this.maxPages - 1)) {
      page -= 1;
    }

    if (page > this.pageNum) {
      this.pagingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    }

    this.setPageNum(page);
    this.goToPageEvent.next(page);
    this.render();
  }

  promptForPage() {
    const goToPageNum = window.prompt('There are ' + this.maxPages + ' pages. What page would you like to go to?', '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  toggleColorMode() {
    switch(this.colorMode) {
      case COLOR_FILTER.NONE:
        this.colorMode = COLOR_FILTER.DARK;
        break;
      case COLOR_FILTER.DARK:
        this.colorMode = COLOR_FILTER.SEPIA;
        break;
      case COLOR_FILTER.SEPIA:
        this.colorMode = COLOR_FILTER.NONE;
        break;
    }
  }

  toggleReaderMode() {
    switch(this.readerMode) {
      case READER_MODE.MANGA_LR:
        this.readerMode = READER_MODE.MANGA_UD;
        this.pagingDirection = PAGING_DIRECTION.FORWARD;
        break;
      case READER_MODE.MANGA_UD:
        this.readerMode = READER_MODE.WEBTOON;
        break;
      case READER_MODE.WEBTOON:
        this.readerMode = READER_MODE.MANGA_LR;
        break;
    }

    this.updateForm();

    this.render();
  }

  updateForm() {
    if ( this.readerMode === READER_MODE.WEBTOON) {
      this.generalSettingsForm.get('fittingOption')?.disable()
      this.generalSettingsForm.get('pageSplitOption')?.disable();
    } else {
      this.generalSettingsForm.get('fittingOption')?.enable()
      this.generalSettingsForm.get('pageSplitOption')?.enable();
    }
  }

  handleWebtoonPageChange(updatedPageNum: number) {
    this.setPageNum(updatedPageNum);
  }

  /**
   * Bookmarks the current page for the chapter
   */
  bookmarkPage() {
    const pageNum = this.pageNum;
    if (this.pageBookmarked) {
      this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, pageNum).pipe(take(1)).subscribe(() => {
        delete this.bookmarks[pageNum];
      });
    } else {
      this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum).pipe(take(1)).subscribe(() => {
        this.bookmarks[pageNum] = 1;
      });
    }

    // Show an effect on the image to show that it was bookmarked
    this.showBookmarkEffectEvent.next(pageNum);
    if (this.readerMode != READER_MODE.WEBTOON) {
      if (this.canvas) {
        this.renderer.addClass(this.canvas?.nativeElement, 'bookmark-effect');
        setTimeout(() => {
          this.renderer.removeClass(this.canvas?.nativeElement, 'bookmark-effect');
        }, 1000);
      }
    }
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
  turnOffIncognito() {
    this.incognitoMode = false;
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info('Incognito mode is off. Progress will now start being tracked.');
    this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});
  }
}
