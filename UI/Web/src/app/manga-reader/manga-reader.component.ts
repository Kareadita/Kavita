import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, HostListener, Inject, OnDestroy, OnInit, Renderer2, SimpleChanges, ViewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ReaderService } from '../_services/reader.service';
import { FormBuilder, FormGroup } from '@angular/forms';
import { NavService } from '../_services/nav.service';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { PageSplitOption } from '../_models/preferences/page-split-option';
import { BehaviorSubject, forkJoin, fromEvent, ReplaySubject, Subject } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { Breakpoint, KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { MemberService } from '../_services/member.service';
import { Stack } from '../shared/data-structures/stack';
import { ChangeContext, LabelType, Options } from '@angular-slider/ngx-slider';
import { trigger, state, style, transition, animate } from '@angular/animations';
import { FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART } from './_models/reader-enums';
import { layoutModes, pageSplitOptions, scalingOptions } from '../_models/preferences/preferences';
import { ReaderMode } from '../_models/preferences/reader-mode';
import { MangaFormat } from '../_models/manga-format';
import { LibraryType } from '../_models/library';
import { ShortcutsModalComponent } from '../reader-shared/_modals/shortcuts-modal/shortcuts-modal.component';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { LayoutMode } from './_models/layout-mode';

const PREFETCH_PAGES = 10;

const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

const ANIMATION_SPEED = 200;
const OVERLAY_AUTO_CLOSE_TIME = 3000;
const CLICK_OVERLAY_TIMEOUT = 3000;



@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
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


  @ViewChild('reader') reader!: ElementRef;
  @ViewChild('readingArea') readingArea!: ElementRef;
  @ViewChild('content') canvas: ElementRef | undefined;
  @ViewChild('image') image!: ElementRef;


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
   * If this is true, we are reading a bookmark. ChapterId will be 0. There is no continuous reading. Progress is not saved. Bookmark control is removed.
   */
  bookmarkMode: boolean = false;

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
  isFullscreen: boolean = false;
  autoCloseMenu: boolean = true;
  readerMode: ReaderMode = ReaderMode.LeftRight;

  pageSplitOptions = pageSplitOptions;
  layoutModes = layoutModes;

  isLoading = true;
  hasBookmarkRights: boolean = false;

  private ctx!: CanvasRenderingContext2D;
  /**
   * Used to render a page on the canvas or in the image tag. This Image element is prefetched by the cachedImages buffer.
   * @remarks Used for rendering to screen.
   */
  canvasImage = new Image();
  /**
   * Used solely for LayoutMode.Double rendering. 
   * @remarks Used for rendering to screen.
   */
  canvasImage2 = new Image();
  /**
   * Used solely for LayoutMode.Double rendering. Will always hold the previous image to canvasImage
   * @see canvasImage
   */
  canvasImagePrev = new Image();
  /**
   * Used solely for LayoutMode.Double rendering. Will always hold the next image to canvasImage
   * @see canvasImage
   */
  canvasImageNext = new Image();
  /**
   * Responsible to hold current page + 2. Used to know if we should render 
   * @remarks Used solely for LayoutMode.DoubleReverse rendering. 
   */
   canvasImageAheadBy2 = new Image();
   /**
   * Responsible to hold current page -2 2. Used to know if we should render 
   * @remarks Used solely for LayoutMode.DoubleReverse rendering. 
   */
  canvasImageBehindBy2 = new Image();
  /**
   * Dictates if we use render with canvas or with image. 
   * @remarks This is only for Splitting.
   */
  renderWithCanvas: boolean = false;

  /**
   * A circular array of size PREFETCH_PAGES. Maintains prefetched Images around the current page to load from to avoid loading animation.
   * @see CircularArray
   */
  cachedImages!: Array<HTMLImageElement>;
  /**
   * A stack of the chapter ids we come across during continuous reading mode. When we traverse a boundary, we use this to avoid extra API calls.
   * @see Stack
   */
  continuousChaptersStack: Stack<number> = new Stack();

  /**
   * An event emitter when a page change occurs. Used solely by the webtoon reader.
   */
  goToPageEvent!: BehaviorSubject<number>;

   /**
   * An event emitter when a bookmark on a page change occurs. Used solely by the webtoon reader.
   */
  showBookmarkEffectEvent: ReplaySubject<number> = new ReplaySubject<number>();
   /**
   * An event emitter when fullscreen mode is toggled. Used solely by the webtoon reader.
   */
  fullscreenEvent: ReplaySubject<boolean> = new ReplaySubject<boolean>();
  /**
   * If the menu is open/visible.
   */
  menuOpen = false;
  /**
   * Image Viewer collapsed
   */
  imageViewerCollapsed = true;
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
   * If extended settings area is visible. Blocks auto-closing of menu.
   */
  settingsOpen: boolean = false;
  /**
   * A map of bookmarked pages to anything. Used for O(1) lookup time if a page is bookmarked or not.
   */
  bookmarks: {[key: string]: number} = {};
  /**
   * Library Type used for rendering chapter or issue
   */
  libraryType: LibraryType = LibraryType.Manga;
  /**
   * Used for webtoon reader. When loading pages or data, this will disable the reader
   */
  inSetup: boolean = true;
  /**
   * If we render 2 pages at once or 1
   */
  layoutMode: LayoutMode = LayoutMode.Single;
  /**
   * Background color for canvas/reader. User configured.
   */
  backgroundColor: string = '#FFFFFF';

  /**
   * This is here as absolute layout requires us to calculate a negative right property for the right pagination when there is overflow. This is calculated on scroll.
   */
  rightPaginationOffset = 0;

  bookmarkPageHandler = this.bookmarkPage.bind(this);

  getPageUrl = (pageNum: number) => {
    if (this.bookmarkMode) return this.readerService.getBookmarkPageUrl(this.seriesId, this.user.apiKey, pageNum);
    return this.readerService.getPageUrl(this.chapterId, pageNum);
  }

  private readonly onDestroy = new Subject<void>();

  get PageNumber() {
    return Math.max(Math.min(this.pageNum, this.maxPages - 1), 0);
  }

  /**
   * Determines if we should render a double page.
   * The general gist is if we are on double layout mode, the current page (first page) is not a cover image or a wide image 
   * and the next page is not a wide image (as only non-wides should be shown next to each other).
   * @remarks This will always fail if the window's width is greater than the height
   */
  get ShouldRenderDoublePage() {
    if (this.layoutMode !== LayoutMode.Double) return false;

    return !(
      this.isCoverImage()
      || this.isWideImage(this.canvasImage)
      || this.isWideImage(this.canvasImageNext)
      );
  }

  /**
   * We should Render 2 pages if:
   *   1. We are not currently the first image (cover image)
   *   2. The previous page is not a cover image
   *   3. The current page is not a wide image
   *   4. The next page is not a wide image
   */
  get ShouldRenderReverseDouble() {
    if (this.layoutMode !== LayoutMode.DoubleReversed) return false;

    const result =  !(
      this.isCoverImage() 
      || this.isCoverImage(this.pageNum - 1)  // This is because we use prev page and hence the cover will re-show
      || this.isWideImage(this.canvasImage) 
      || this.isWideImage(this.canvasImageNext)
      );
    
    return result;
  }

  get CurrentPageBookmarked() {
    return this.bookmarks.hasOwnProperty(this.pageNum);
  }

  get WindowWidth() {
    return this.readingArea?.nativeElement.scrollWidth + 'px';
  }

  get WindowHeight() {
    return this.readingArea?.nativeElement.scrollHeight + 'px';
  }

  get ImageWidth() {
    return this.image?.nativeElement.width + 'px';
  }

  get ImageHeight() {
    // If we are a wide image and implied fit to screen, then we need to take screen height rather than image height
    if (this.isWideImage() || this.FittingOption === FITTING_OPTION.WIDTH) {
      return this.WindowHeight;
    }
    return Math.max(this.readingArea?.nativeElement?.clientHeight, this.image?.nativeElement.height) + 'px';
  }

  get RightPaginationOffset() {
    if (this.readerMode === ReaderMode.LeftRight && this.FittingOption === FITTING_OPTION.HEIGHT) {
      return (this.readingArea?.nativeElement?.scrollLeft || 0) * -1;
    }
    return 0;
  }

  get SplitIconClass() {
    if (this.isSplitLeftToRight()) {
      return 'left-side';
    } else if (this.isNoSplit()) {
      return 'none';
    }
    return 'right-side';
  }


  get ReaderMode() {
    return ReaderMode;
  }
  get LayoutMode() {
    return LayoutMode;
  }

  get ReadingDirection() {
    return ReadingDirection;
  }

  get PageSplitOption() {
    return PageSplitOption;
  }

  get Breakpoint() {
    return Breakpoint;
  }

  get FITTING_OPTION() {
    return FITTING_OPTION;
  }

  get FittingOption() {
    return this.generalSettingsForm.get('fittingOption')?.value;
  }

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
              public readerService: ReaderService, private formBuilder: FormBuilder, private navService: NavService,
              private toastr: ToastrService, private memberService: MemberService,
              public utilityService: UtilityService, private renderer: Renderer2,
              @Inject(DOCUMENT) private document: Document, private modalService: NgbModal,
              private readonly cdRef: ChangeDetectorRef) {
                this.navService.hideNavBar();
                this.navService.hideSideNav();
                this.cdRef.markForCheck();
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
    this.bookmarkMode = this.route.snapshot.queryParamMap.get('bookmarkMode') === 'true';

    const readingListId = this.route.snapshot.queryParamMap.get('readingListId');
    if (readingListId != null) {
      this.readingListMode = true;
      this.readingListId = parseInt(readingListId, 10);
    }

    this.continuousChaptersStack.push(this.chapterId);

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) {
        this.router.navigateByUrl('/login');
        return;
      }

      this.user = user;
      this.hasBookmarkRights = this.accountService.hasBookmarkRole(user) || this.accountService.hasAdminRole(user);
      this.readingDirection = this.user.preferences.readingDirection;
      this.scalingOption = this.user.preferences.scalingOption;
      this.pageSplitOption = this.user.preferences.pageSplitOption;
      this.autoCloseMenu = this.user.preferences.autoCloseMenu;
      this.readerMode = this.user.preferences.readerMode;
      this.layoutMode = this.user.preferences.layoutMode || LayoutMode.Single;
      this.backgroundColor = this.user.preferences.backgroundColor || '#000000';
      this.readerService.setOverrideStyles(this.backgroundColor);

      this.generalSettingsForm = this.formBuilder.group({
        autoCloseMenu: this.autoCloseMenu,
        pageSplitOption: this.pageSplitOption,
        fittingOption: this.translateScalingOption(this.scalingOption),
        layoutMode: this.layoutMode,
        darkness: 100
      });

      this.updateForm();

      this.generalSettingsForm.get('layoutMode')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(val => {

        const changeOccurred = parseInt(val, 10) !== this.layoutMode;
        this.layoutMode = parseInt(val, 10);

        if (this.layoutMode === LayoutMode.Single) {
          this.generalSettingsForm.get('pageSplitOption')?.enable();
          this.generalSettingsForm.get('fittingOption')?.enable();
        } else {
          this.generalSettingsForm.get('pageSplitOption')?.setValue(PageSplitOption.NoSplit);
          this.generalSettingsForm.get('pageSplitOption')?.disable();
          this.generalSettingsForm.get('fittingOption')?.setValue(this.translateScalingOption(ScalingOption.FitToHeight));
          this.generalSettingsForm.get('fittingOption')?.disable();
        }
        this.cdRef.markForCheck();

        // Re-render the current page when we switch layouts
        if (changeOccurred) {
          this.loadPage();
        }
      });

      this.generalSettingsForm.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((changes: SimpleChanges) => {
        this.autoCloseMenu = this.generalSettingsForm.get('autoCloseMenu')?.value;
        const needsSplitting = this.isWideImage();
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
    });

    this.init();
  }

  ngAfterViewInit() {
    fromEvent(this.readingArea.nativeElement, 'scroll').pipe(debounceTime(20), takeUntil(this.onDestroy)).subscribe(evt => {
      if (this.readerMode === ReaderMode.Webtoon) return;
      if (this.readerMode === ReaderMode.LeftRight && this.FittingOption === FITTING_OPTION.HEIGHT) {
        this.rightPaginationOffset = (this.readingArea.nativeElement.scrollLeft) * -1;
        this.cdRef.markForCheck();
        return;
      }
      this.rightPaginationOffset = 0;
      this.cdRef.markForCheck();
    });

    fromEvent(this.readingArea.nativeElement, 'click').pipe(debounceTime(200)).subscribe((event: MouseEvent | any) => {
      if (event.detail > 1) return;
      this.toggleMenu();
    });

    if (this.canvas) {
      this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });
      this.canvasImage.onload = () => this.renderPage();
    }
  }

  ngOnDestroy() {
    this.readerService.resetOverrideStyles();
    this.navService.showNavBar();
    this.navService.showSideNav();
    this.onDestroy.next();
    this.onDestroy.complete();
    this.showBookmarkEffectEvent.complete();
    this.readerService.exitFullscreen();
    if (this.goToPageEvent !== undefined) this.goToPageEvent.complete();
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize() {  
    if (window.innerWidth > window.innerHeight) {
      this.generalSettingsForm.get('layoutMode')?.enable();
      this.cdRef.markForCheck();
      return;
    };
    if (this.layoutMode === LayoutMode.Single || this.readerMode === ReaderMode.Webtoon) return;
    
    this.generalSettingsForm.get('layoutMode')?.setValue(LayoutMode.Single);
    this.generalSettingsForm.get('layoutMode')?.disable();
    this.toastr.info('Layout mode switched to Single due to insufficient space to render double layout');
    this.cdRef.markForCheck();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    switch (this.readerMode) {
      case ReaderMode.LeftRight:
        if (event.key === KEY_CODES.RIGHT_ARROW) {
          //if (!this.checkIfPaginationAllowed()) return;
          this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
        } else if (event.key === KEY_CODES.LEFT_ARROW) {
          //if (!this.checkIfPaginationAllowed()) return;
          this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
        }
        break;
      case ReaderMode.UpDown:
      case ReaderMode.Webtoon:
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
    } else if (event.key === KEY_CODES.F) {
      this.toggleFullscreen();
    } else if (event.key === KEY_CODES.H) {
      this.openShortcutModal();
    }
  }

  // if there is scroll room and on original, then don't paginate
  checkIfPaginationAllowed() {
    // This is not used atm due to the complexity it adds with keyboard.
    if (this.readingArea === undefined || this.readingArea.nativeElement === undefined) return true;

    const scrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
    const totalScrollWidth = this.readingArea?.nativeElement?.scrollWidth;
    // need to also check if there is scroll needed

    if (this.FittingOption === FITTING_OPTION.ORIGINAL && scrollLeft < totalScrollWidth) {
      return false;
    }
    return true;
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

  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.pageNum = 0;
    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    this.inSetup = true;
    this.canvasImage.src = '';
    this.canvasImage2.src = '';
    this.cdRef.markForCheck();

    if (this.goToPageEvent) {
      // There was a bug where goToPage was emitting old values into infinite scroller between chapter loads. We explicity clear it out between loads
      // and we use a BehaviourSubject to ensure only latest value is sent
      this.goToPageEvent.complete();
    }

    if (this.bookmarkMode) {
      this.readerService.getBookmarkInfo(this.seriesId).subscribe(bookmarkInfo => {
        this.setPageNum(0);
        this.title = bookmarkInfo.seriesName;
        this.subtitle = 'Bookmarks';
        this.libraryType = bookmarkInfo.libraryType;
        this.maxPages = bookmarkInfo.pages;

        // Due to change detection rules in Angular, we need to re-create the options object to apply the change
        const newOptions: Options = Object.assign({}, this.pageOptions);
        newOptions.ceil = this.maxPages - 1; // We -1 so that the slider UI shows us hitting the end, since visually we +1 everything.
        this.pageOptions = newOptions;
        this.inSetup = false;
        this.cdRef.markForCheck();

        this.cachedImages = [];
        for (let i = 0; i < PREFETCH_PAGES; i++) {
          this.cachedImages.push(new Image())
        }

        this.goToPageEvent = new BehaviorSubject<number>(this.pageNum);

        this.render();
      });

      return;
    }

    forkJoin({
      progress: this.readerService.getProgress(this.chapterId),
      chapterInfo: this.readerService.getChapterInfo(this.chapterId),
      bookmarks: this.readerService.getBookmarks(this.chapterId),
    }).pipe(take(1)).subscribe(results => {
      if (this.readingListMode && (results.chapterInfo.seriesFormat === MangaFormat.EPUB || results.chapterInfo.seriesFormat === MangaFormat.PDF)) {
        // Redirect to the book reader.
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(this.readerService.getNavigationArray(results.chapterInfo.libraryId, results.chapterInfo.seriesId, this.chapterId, results.chapterInfo.seriesFormat), {queryParams: params});
        return;
      }

      this.volumeId = results.chapterInfo.volumeId;
      this.maxPages = results.chapterInfo.pages;
      let page = results.progress.pageNum;
      if (page > this.maxPages) {
        page = this.maxPages - 1;
      }
      this.setPageNum(page);
      this.goToPageEvent = new BehaviorSubject<number>(this.pageNum);




      // Due to change detection rules in Angular, we need to re-create the options object to apply the change
      const newOptions: Options = Object.assign({}, this.pageOptions);
      newOptions.ceil = this.maxPages - 1; // We -1 so that the slider UI shows us hitting the end, since visually we +1 everything.
      this.pageOptions = newOptions;

      this.libraryType = results.chapterInfo.libraryType;
      this.title = results.chapterInfo.title;
      this.subtitle = results.chapterInfo.subtitle;

      this.inSetup = false;



      // From bookmarks, create map of pages to make lookup time O(1)
      this.bookmarks = {};
      results.bookmarks.forEach(bookmark => {
        this.bookmarks[bookmark.page] = 1;
      });
      this.cdRef.markForCheck();

      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
          this.nextChapterDisabled = true;
          this.cdRef.markForCheck();
        }
      });
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
          this.prevChapterDisabled = true;
          this.cdRef.markForCheck();
        }
      });

      this.cachedImages = [];
      for (let i = 0; i < PREFETCH_PAGES; i++) {
        this.cachedImages.push(new Image());
      }


      this.render();
    }, () => {
      setTimeout(() => {
        this.closeReader();
      }, 200);
    });
  }

  closeReader() {
    this.readerService.closeReader(this.readingListMode, this.readingListId);
  }

  render() {
    if (this.readerMode === ReaderMode.Webtoon) {
      this.isLoading = false;
      this.cdRef.markForCheck();
    } else {
      this.loadPage();
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
    let val = FITTING_OPTION.HEIGHT;
    if (formControl === undefined) {
      val =  FITTING_OPTION.HEIGHT;
    }
    val =  formControl?.value;

    if (
      this.isWideImage() &&
      this.layoutMode === LayoutMode.Single &&
      val !== FITTING_OPTION.WIDTH &&
      this.shouldRenderAsFitSplit()
      ) {
      // Rewriting to fit to width for this cover image
      return FITTING_OPTION.WIDTH;
    }

    if (this.isWideImage() && this.layoutMode !== LayoutMode.Single) {
      return val + ' wide double';
    }

    if (this.isCoverImage() && this.layoutMode !== LayoutMode.Single) {
      return val + ' cover double';
    }

    return val;
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
    this.cdRef.markForCheck();

    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
    }

    if (this.menuOpen && !this.settingsOpen) {
      this.startMenuCloseTimer();
    } else {
      this.showClickOverlay = false;
      this.settingsOpen = false;
      this.cdRef.markForCheck();
    }
  }

  isSplitLeftToRight() {
    return parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10) === PageSplitOption.SplitLeftToRight;
  }

  /**
   *
   * @returns If the current model reflects no split of fit split
   * @remarks Fit to Screen falls under no split
   */
  isNoSplit() {
    const splitValue = parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10);
    return splitValue === PageSplitOption.NoSplit || splitValue === PageSplitOption.FitSplit;
  }

  updateSplitPage() {
    const needsSplitting = this.isWideImage();
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
    if (this.readerMode === ReaderMode.Webtoon) {
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

    let pageAmount = 1;

    // If we are on the cover image, always do 1 page
    
    if (!this.isCoverImage()) {
      if (this.layoutMode === LayoutMode.Double) {
        pageAmount = (
          !this.isCoverImage() &&
          !this.isWideImage() &&
          !this.isWideImage(this.canvasImageNext) &&
          !this.isSecondLastImage() &&
          !this.isLastImage()
          ? 2 : 1);
      } else if (this.layoutMode === LayoutMode.DoubleReversed) {
        // Move forward by 1 pages if:
        // 1. The next page is a wide image
        // 2. The next page + 1 is a wide image (why do we care at this point?)
        // 3. We are on the second to last page
        // 4. We are on the last page
        pageAmount = !(
          this.isWideImage(this.canvasImageNext)  
          || this.isWideImage(this.canvasImageAheadBy2)  // Remember we are doing this logic before we've hit the next page, so we need this
          || this.isSecondLastImage()
          || this.isLastImage()
          ) ? 2 : 1;
      }
    }


    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART);
    if ((this.pageNum + pageAmount >= this.maxPages && notInSplit) || this.isLoading) {

      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum + pageAmount);
    }

    this.loadPage();
  }

  prevPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART);

    let pageAmount = 1;
    if (this.layoutMode === LayoutMode.Double) {
      pageAmount = !(
        this.isCoverImage()
        || this.isWideImage(this.canvasImagePrev)
      ) ? 2 : 1;
    } else if (this.layoutMode === LayoutMode.DoubleReversed) {
      pageAmount = !(
        this.isCoverImage() 
        || this.isCoverImage(this.pageNum - 1) 
        || this.isWideImage(this.canvasImage)  // JOE: At this point, these aren't yet set to the new values
        || this.isWideImage(this.canvasImagePrev) // This should be Prev, if prev image  (original: canvasImageNext)
      ) ? 2 : 1;
    }

    if ((this.pageNum - 1 < 0 && notInSplit) || this.isLoading) {
      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum - pageAmount);
    }

    this.loadPage();
  }

  /**
   * Sets canvasImage's src to current page, but first attempts to use a pre-fetched image
   */
  setCanvasImage() {
    if (this.layoutMode === LayoutMode.Single) {
      const img = this.cachedImages.find(img => this.readerService.imageUrlToPageNum(img.src) === this.pageNum);
      if (img) {
        this.canvasImage = img; // If we tried to use this for double, then the loadPage might not render correctly when switching layout mode
        console.log('Using prefetched image');
      } else {
        this.canvasImage.src = this.getPageUrl(this.pageNum);
        console.log('Using new image');
      }
    } else {
      this.canvasImage.src = this.getPageUrl(this.pageNum);
    }

    
    this.canvasImage.onload = () => {
      console.log('Canvas Image Onload'); // I never see this fire
      this.cdRef.markForCheck();
    };
    
    this.cdRef.markForCheck();
  }

  loadNextChapter() {
    if (this.nextPageDisabled || this.nextChapterDisabled || this.bookmarkMode) { 
      this.toastr.info('No Next Chapter');
      return;
     }

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
    if (this.prevPageDisabled || this.prevChapterDisabled || this.bookmarkMode) { 
      this.toastr.info('No Previous Chapter');
      return; 
    }
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
    if (chapterId > 0) {
      this.isLoading = true;
      this.cdRef.markForCheck();

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
      this.cdRef.markForCheck();
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
      } else {
        this.canvas.nativeElement.width = this.canvasImage.width;
        this.canvas.nativeElement.height = this.canvasImage.height;
      }
      this.cdRef.markForCheck();
    }
  }

  renderPage() {
    const needsSplitting = this.isWideImage();
    
    if (!this.ctx || !this.canvas || this.isNoSplit() || !needsSplitting) {
      this.renderWithCanvas = false;
      if (this.getFit() !== FITTING_OPTION.HEIGHT) {
        this.readingArea.nativeElement.scroll(0,0);
      }
      this.isLoading = false;
      this.cdRef.markForCheck();
      return;
    }

    this.renderWithCanvas = true;
    this.canvasImage.onload = null;

    this.setCanvasSize();
    this.updateSplitPage();

    if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    } else if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
      this.canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
      this.cdRef.markForCheck();
    }

    // Reset scroll on non HEIGHT Fits
    if (this.getFit() !== FITTING_OPTION.HEIGHT) {
      this.readingArea.nativeElement.scroll(0,0);
    }
    this.isLoading = false;
    this.cdRef.markForCheck();
  }

  updateScalingForFirstPageRender() {
    const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
    const windowHeight = window.innerHeight
              || document.documentElement.clientHeight
              || document.body.clientHeight;

      const needsSplitting = this.isWideImage();
      let newScale = this.FittingOption;
      const widthRatio = windowWidth / (this.canvasImage.width / (needsSplitting ? 2 : 1));
      const heightRatio = windowHeight / (this.canvasImage.height);

      // Given that we now have image dimensions, assuming this isn't a split image,
      // Try to reset one time based on who's dimension (width/height) is smaller
      if (widthRatio < heightRatio) {
        newScale = FITTING_OPTION.WIDTH;
      } else if (widthRatio > heightRatio) {
        newScale = FITTING_OPTION.HEIGHT;
      }

      this.generalSettingsForm.get('fittingOption')?.setValue(newScale, {emitEvent: false});
  }

  /**
   * If pagenumber is 0 aka first page, which on double page rendering should always render as a single. 
   * 
   * @param pageNumber Defaults to current page number
   * @returns 
   */
  isCoverImage(pageNumber = this.pageNum) {
    return pageNumber === 0;
  }

  /**
   * If the image's width is greater than it's height
   * @param elem Optional Image
   */
  isWideImage(elem?: HTMLImageElement) {
    if (elem) {
      elem.onload = () => {
        return elem.width > elem.height;
      }
      if (elem.src === '') return false;
    }
    const element = elem || this.canvasImage;
    return element.width > element.height;
  }

  /**
   * If the current page is second to last image
   */
  isSecondLastImage() {
    return this.maxPages - 1 - this.pageNum === 1;
  }

  /**
   * If the current image is last image
   */
  isLastImage() {
    return this.maxPages - 1 === this.pageNum;
  }

  shouldRenderAsFitSplit() {
    // Some pages aren't cover images but might need fit split renderings
    if (parseInt(this.generalSettingsForm?.get('pageSplitOption')?.value, 10) !== PageSplitOption.FitSplit) return false;
    return true;
  }

  /**
   * Maintains an array of images (that are requested from backend) around the user's current page. This allows for quick loading (seemless to user)
   * and also maintains page info (wide image, etc) due to onload event.
   */
  prefetch() {
    // NOTE: This doesn't allow for any directionality
    // NOTE: This doesn't maintain 1 image behind at all times
    for(let i = 0; i <= PREFETCH_PAGES - 3; i++) {
      const numOffset = this.pageNum + i;
      if (numOffset > this.maxPages - 1) continue;

      const index = (numOffset % this.cachedImages.length + this.cachedImages.length) % this.cachedImages.length;
      if (this.readerService.imageUrlToPageNum(this.cachedImages[index].src) !== numOffset) {
        this.cachedImages[index].src = this.getPageUrl(numOffset);
        this.cachedImages[index].onload = () => {
          console.log('Page ', numOffset, ' loaded');
          this.cdRef.markForCheck();
        };
      }
    }

    const pages = this.cachedImages.map(img => this.readerService.imageUrlToPageNum(img.src));
    const pagesBefore = pages.filter(p => p >= 0 && p < this.pageNum).length;
    const pagesAfter = pages.filter(p => p >= 0 && p > this.pageNum).length;
    console.log('Buffer Health: Before: ', pagesBefore, ' After: ', pagesAfter);
    console.log(this.pageNum, ' Prefetched pages: ', pages.map(p => {
      if (this.pageNum === p) return '[' + p + ']';
      return '' + p
    }));
  }


  loadPage() {
    if (this.readerMode === ReaderMode.Webtoon) return;
    
    this.isLoading = true;
    this.canvasImage2.src = '';
    this.canvasImageAheadBy2.src = '';


    this.setCanvasImage();


    if (this.layoutMode !== LayoutMode.Single) {
      this.canvasImageNext.src = this.getPageUrl(this.pageNum + 1); // This needs to be capped at maxPages !this.isLastImage()
      this.canvasImagePrev.src = this.getPageUrl(this.pageNum - 1);

      if (this.pageNum + 2 < this.maxPages - 1) {
        this.canvasImageAheadBy2.src = this.getPageUrl(this.pageNum + 2);
      }
      if (this.pageNum - 2 >= 0) {
        this.canvasImageBehindBy2.src = this.getPageUrl(this.pageNum - 2 || 0);
      }      
    
      if (this.ShouldRenderDoublePage || this.ShouldRenderReverseDouble) {
        if (this.layoutMode === LayoutMode.Double) {
          this.canvasImage2.src = this.canvasImageNext.src;
        } else {
          this.canvasImage2.src = this.canvasImagePrev.src;
        }
      }
    }
    this.cdRef.markForCheck();

    this.renderPage();
    this.prefetch();
    this.isLoading = false;
    this.cdRef.markForCheck();
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }

    if (this.menuOpen && this.user.preferences.showScreenHints) {
      this.showClickOverlay = true;
      setTimeout(() => {
        this.showClickOverlay = false;
      }, CLICK_OVERLAY_TIMEOUT);
    }
  }


  sliderDragUpdate(context: ChangeContext) {
    // This will update the value for value except when in webtoon due to how the webtoon reader
    // responds to page changes
    if (this.readerMode !== ReaderMode.Webtoon) {
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
    this.pageNum = Math.max(Math.min(pageNum, this.maxPages - 1), 0);
    this.cdRef.markForCheck();

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
    if (this.pageNum == this.maxPages - 1 && this.pagingDirection === PAGING_DIRECTION.FORWARD) {
      tempPageNum = this.pageNum + 1;
    }

    if (!this.incognitoMode && !this.bookmarkMode) {
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

  toggleFullscreen() {
    this.isFullscreen = this.readerService.checkFullscreenMode();
    if (this.isFullscreen) {
      this.readerService.exitFullscreen(() => {
        this.isFullscreen = false;
        this.fullscreenEvent.next(false);
        this.render();
      });
    } else {
      this.readerService.enterFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = true;
        this.fullscreenEvent.next(true);
        this.render();
      });
    }
  }


  toggleReaderMode() {
    switch(this.readerMode) {
      case ReaderMode.LeftRight:
        this.readerMode = ReaderMode.UpDown;
        this.pagingDirection = PAGING_DIRECTION.FORWARD;
        break;
      case ReaderMode.UpDown:
        this.readerMode = ReaderMode.Webtoon;
        break;
      case ReaderMode.Webtoon:
        this.readerMode = ReaderMode.LeftRight;
        break;
    }

    // We must set this here because loadPage from render doesn't call if we aren't page splitting
    if (this.readerMode !== ReaderMode.Webtoon) {
      this.canvasImage = this.cachedImages[this.pageNum & this.cachedImages.length];
      this.isLoading = true;
    }

    this.updateForm();

    this.render();
  }

  updateForm() {
    if ( this.readerMode === ReaderMode.Webtoon) {
      this.generalSettingsForm.get('pageSplitOption')?.disable()
      this.generalSettingsForm.get('fittingOption')?.disable()
      this.generalSettingsForm.get('layoutMode')?.disable();
    } else {
      this.generalSettingsForm.get('fittingOption')?.enable()
      this.generalSettingsForm.get('pageSplitOption')?.enable();
      this.generalSettingsForm.get('layoutMode')?.enable();

      if (this.layoutMode !== LayoutMode.Single) {
        this.generalSettingsForm.get('pageSplitOption')?.disable();
        this.generalSettingsForm.get('fittingOption')?.disable();
      }
    }
    this.cdRef.markForCheck();
  }

  handleWebtoonPageChange(updatedPageNum: number) {
    this.setPageNum(updatedPageNum);
  }

  /**
   * Bookmarks the current page for the chapter
   */
  bookmarkPage(event: MouseEvent | undefined = undefined) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.bookmarkMode) return;

    const pageNum = this.pageNum;
    const isDouble = this.layoutMode === LayoutMode.Double || this.layoutMode === LayoutMode.DoubleReversed;

    if (this.CurrentPageBookmarked) {
      let apis = [this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, pageNum)];
      if (isDouble) apis.push(this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, pageNum + 1));
      forkJoin(apis).pipe(take(1)).subscribe(() => {
        delete this.bookmarks[pageNum];
        if (isDouble) delete this.bookmarks[pageNum + 1];
      });
    } else {
      let apis = [this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum)];
      if (isDouble) apis.push(this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum + 1));
      forkJoin(apis).pipe(take(1)).subscribe(() => {
        this.bookmarks[pageNum] = 1;
        if (isDouble) this.bookmarks[pageNum + 1] = 1;
      });
    }

    // Show an effect on the image to show that it was bookmarked
    this.showBookmarkEffectEvent.next(pageNum);
    if (this.readerMode === ReaderMode.Webtoon) return;

    let elements:Array<Element | ElementRef> = [];
    if (this.renderWithCanvas && this.canvas) {
      elements.push(this.canvas?.nativeElement);
    } else {
      const image1 = this.document.querySelector('#image-1');
      if (image1 != null) elements.push(image1);

      if (this.layoutMode !== LayoutMode.Single) {
        const image2 = this.document.querySelector('#image-2');
        if (image2 != null) elements.push(image2);
      }
    }


    if (elements.length > 0) {
      elements.forEach(elem => this.renderer.addClass(elem, 'bookmark-effect'));
      setTimeout(() => {
        elements.forEach(elem => this.renderer.removeClass(elem, 'bookmark-effect'));
      }, 1000);
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
    if (!this.bookmarkMode) {
      this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});
    }
  }

  openShortcutModal() {
    let ref = this.modalService.open(ShortcutsModalComponent, { scrollable: true, size: 'md' });
    ref.componentInstance.shortcuts = [
      {key: '⇽', description: 'Move to previous page'},
      {key: '⇾', description: 'Move to next page'},
      {key: '↑', description: 'Move to previous page'},
      {key: '↓', description: 'Move to previous page'},
      {key: 'G', description: 'Open Go to Page dialog'},
      {key: 'B', description: 'Bookmark current page'},
      {key: 'ESC', description: 'Close reader'},
      {key: 'SPACE', description: 'Toggle Menu'},
    ];
  }
}
