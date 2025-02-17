import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  OnDestroy,
  OnInit,
  ViewChild
} from '@angular/core';
import {AsyncPipe, NgClass, NgStyle, PercentPipe} from '@angular/common';
import {ActivatedRoute, Router} from '@angular/router';
import {
  BehaviorSubject,
  debounceTime,
  distinctUntilChanged,
  filter,
  forkJoin,
  fromEvent,
  map,
  merge,
  Observable,
  ReplaySubject,
  Subject,
  take,
  tap
} from 'rxjs';
import {ChangeContext, LabelType, NgxSliderModule, Options} from '@angular-slider/ngx-slider';
import {animate, state, style, transition, trigger} from '@angular/animations';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {ShortcutsModalComponent} from 'src/app/reader-shared/_modals/shortcuts-modal/shortcuts-modal.component';
import {Stack} from 'src/app/shared/data-structures/stack';
import {Breakpoint, KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {LibraryType} from 'src/app/_models/library/library';
import {MangaFormat} from 'src/app/_models/manga-format';
import {PageSplitOption} from 'src/app/_models/preferences/page-split-option';
import {layoutModes, pageSplitOptions} from 'src/app/_models/preferences/preferences';
import {ReaderMode} from 'src/app/_models/preferences/reader-mode';
import {ReadingDirection} from 'src/app/_models/preferences/reading-direction';
import {ScalingOption} from 'src/app/_models/preferences/scaling-option';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {MemberService} from 'src/app/_services/member.service';
import {NavService} from 'src/app/_services/nav.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {LayoutMode} from '../../_models/layout-mode';
import {FITTING_OPTION, PAGING_DIRECTION} from '../../_models/reader-enums';
import {ReaderSetting} from '../../_models/reader-setting';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {CanvasRendererComponent} from '../canvas-renderer/canvas-renderer.component';
import {DoubleRendererComponent} from '../double-renderer/double-renderer.component';
import {DoubleReverseRendererComponent} from '../double-reverse-renderer/double-reverse-renderer.component';
import {SingleRendererComponent} from '../single-renderer/single-renderer.component';
import {ChapterInfo} from '../../_models/chapter-info';
import {DoubleNoCoverRendererComponent} from '../double-renderer-no-cover/double-no-cover-renderer.component';
import {SwipeEvent} from 'src/app/ng-swipe/ag-swipe.core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FullscreenIconPipe} from '../../../_pipes/fullscreen-icon.pipe';
import {ReaderModeIconPipe} from '../../../_pipes/reader-mode-icon.pipe';
import {FittingIconPipe} from '../../../_pipes/fitting-icon.pipe';
import {InfiniteScrollerComponent} from '../infinite-scroller/infinite-scroller.component';
import {SwipeDirective} from '../../../ng-swipe/ng-swipe.directive';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {shareReplay} from "rxjs/operators";
import {DblClickDirective} from "../../../_directives/dbl-click.directive";


const PREFETCH_PAGES = 10;

const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

const ANIMATION_SPEED = 200;
const OVERLAY_AUTO_CLOSE_TIME = 3000;
const CLICK_OVERLAY_TIMEOUT = 3000;

enum ChapterInfoPosition {
  Previous = 0,
  Current = 1,
  Next = 2
}

enum KeyDirection {
  Right = 0,
  Left = 1,
  Up = 2,
  Down = 3
}

@Component({
    selector: 'app-manga-reader',
    templateUrl: './manga-reader.component.html',
    styleUrls: ['./manga-reader.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [MangaReaderService],
    animations: [
        trigger('slideFromTop', [
            state('in', style({ transform: 'translateY(0)' })),
            transition('void => *', [
                style({ transform: 'translateY(-100%)' }),
                animate(ANIMATION_SPEED)
            ]),
            transition('* => void', [
                animate(ANIMATION_SPEED, style({ transform: 'translateY(-100%)' })),
            ])
        ]),
        trigger('slideFromBottom', [
            state('in', style({ transform: 'translateY(0)' })),
            transition('void => *', [
                style({ transform: 'translateY(100%)' }),
                animate(ANIMATION_SPEED)
            ]),
            transition('* => void', [
                animate(ANIMATION_SPEED, style({ transform: 'translateY(100%)' })),
            ])
        ])
    ],
    standalone: true,
  imports: [NgStyle, LoadingComponent, SwipeDirective, CanvasRendererComponent, SingleRendererComponent,
    DoubleRendererComponent, DoubleReverseRendererComponent, DoubleNoCoverRendererComponent, InfiniteScrollerComponent,
    NgxSliderModule, ReactiveFormsModule, FittingIconPipe, ReaderModeIconPipe,
    FullscreenIconPipe, TranslocoDirective, PercentPipe, NgClass, AsyncPipe, DblClickDirective]
})
export class MangaReaderComponent implements OnInit, AfterViewInit, OnDestroy {


  @ViewChild('reader') reader!: ElementRef;
  @ViewChild('readingArea') readingArea!: ElementRef;
  @ViewChild('content') canvas: ElementRef | undefined;

  @ViewChild(CanvasRendererComponent, { static: false }) canvasRenderer!: CanvasRendererComponent;
  @ViewChild(SingleRendererComponent, { static: false }) singleRenderer!: SingleRendererComponent;
  @ViewChild(DoubleRendererComponent, { static: false }) doubleRenderer!: DoubleRendererComponent;
  @ViewChild(DoubleReverseRendererComponent, { static: false }) doubleReverseRenderer!: DoubleReverseRendererComponent;
  @ViewChild(DoubleNoCoverRendererComponent, { static: false }) doubleNoCoverRenderer!: DoubleNoCoverRendererComponent;

  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly navService = inject(NavService);
  private readonly memberService = inject(MemberService);
  private readonly modalService = inject(NgbModal);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  public readonly readerService = inject(ReaderService);
  public readonly utilityService = inject(UtilityService);
  public readonly mangaReaderService = inject(MangaReaderService);

  protected readonly KeyDirection = KeyDirection;
  protected readonly ReaderMode = ReaderMode;
  protected readonly LayoutMode = LayoutMode;
  protected readonly ReadingDirection = ReadingDirection;
  protected readonly Breakpoint = Breakpoint;
  protected readonly Math = Math;

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
  totalSeriesPages = 0;
  totalSeriesPagesRead = 0;
  user!: User;
  generalSettingsForm!: FormGroup;

  readingDirection = ReadingDirection.LeftToRight;
  scalingOption = ScalingOption.FitToHeight;
  pageSplitOption = PageSplitOption.FitSplit;

  isFullscreen: boolean = false;
  autoCloseMenu: boolean = true;

  readerMode: ReaderMode = ReaderMode.LeftRight;
  readerModeSubject = new BehaviorSubject(this.readerMode);
  readerMode$: Observable<ReaderMode> = this.readerModeSubject.asObservable();

  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;
  pagingDirectionSubject: Subject<PAGING_DIRECTION> = new BehaviorSubject(this.pagingDirection);
  pagingDirection$: Observable<PAGING_DIRECTION> = this.pagingDirectionSubject.asObservable();


  pageSplitOptionsTranslated = pageSplitOptions.map(this.translatePrefOptions);
  layoutModesTranslated = layoutModes.map(this.translatePrefOptions);

  isLoading = true;
  hasBookmarkRights: boolean = false; // TODO: This can be an observable


  getPageFn!: (pageNum: number) => HTMLImageElement;


  /**
   * Used to render a page on the canvas or in the image tag. This Image element is prefetched by the cachedImages buffer.
   * @remarks Used for rendering to screen.
   */
  canvasImage = new Image();

  /**
   * A circular array of size PREFETCH_PAGES. Maintains prefetched Images around the current page to load from to avoid loading animation.
   * @see CircularArray
   */
  cachedImages: Array<HTMLImageElement> = [];
  /**
   * A stack of the chapter ids we come across during continuous reading mode. When we traverse a boundary, we use this to avoid extra API calls.
   * @see Stack
   */
  continuousChaptersStack: Stack<number> = new Stack();

  continuousChapterInfos: Array<ChapterInfo | undefined> = [undefined, undefined, undefined];

  /**
   * An event emitter when a page change occurs. Used solely by the webtoon reader.
   */
  goToPageEvent!: BehaviorSubject<number>; // Renderer interaction

   /**
   * An event emitter when a bookmark on a page change occurs. Used solely by the webtoon reader.
   */
  showBookmarkEffectEvent: ReplaySubject<number> = new ReplaySubject<number>();
  showBookmarkEffect$: Observable<number> = this.showBookmarkEffectEvent.asObservable();
   /**
   * An event emitter when fullscreen mode is toggled. Used solely by the webtoon reader.
   */
  fullscreenEvent: ReplaySubject<boolean> = new ReplaySubject<boolean>();
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
    translate: (_: number, label: LabelType) => {
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
  private showClickOverlaySubject: ReplaySubject<boolean> = new ReplaySubject();
  showClickOverlay$ = this.showClickOverlaySubject.asObservable();
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

  /**
   * Previous amount of scroll left. Used for swipe to paginate functionality.
   */
  prevScrollLeft = 0;
  /**
   * Previous amount of scroll top. Used for swipe to paginate functionality.
   */
  prevScrollTop = 0;

  prevIsHorizontalScrollLeft = true;
  prevIsVerticalScrollLeft = true;

  /**
   * Has the user scrolled to the far right side. This is used for swipe to next page and must ensure user is at end of scroll then on next swipe, will move pages.
   */
  hasHitRightScroll = false;
  /**
   * Has the user scrolled once for the current page
   */
  hasScrolledX: boolean = false;
  /**
   * Has the user scrolled once in the Y axis for the current page
   */
  hasScrolledY: boolean = false;
  /**
   * Has the user scrolled to far left size. This doesn't include starting from no scroll
   */
  hasHitZeroScroll: boolean = false;
  /**
   * Has the user scrolled to the far top of the screen
   */
  hasHitZeroTopScroll: boolean = false;
  /**
   * Has the user scrolled to the far bottom of the screen
   */
  hasHitBottomTopScroll: boolean = false;

  /**
   * Show and log debug information
   */
  debugMode: boolean = false;
  /**
   * Width override label for manual width control
  */
  widthOverrideLabel$ : Observable<string> = new Observable<string>();

  // Renderer interaction
  readerSettings$!: Observable<ReaderSetting>;
  private currentImage: Subject<HTMLImageElement | null> = new ReplaySubject(1);
  currentImage$: Observable<HTMLImageElement | null> = this.currentImage.asObservable().pipe(
    shareReplay({refCount: true, bufferSize: 2})
  );

  private pageNumSubject: Subject<{pageNum: number, maxPages: number}> = new ReplaySubject();
  pageNum$: Observable<{pageNum: number, maxPages: number}> = this.pageNumSubject.asObservable();

  getPageUrl = (pageNum: number, chapterId: number = this.chapterId) => {
    if (this.bookmarkMode) return this.readerService.getBookmarkPageUrl(this.seriesId, this.user.apiKey, pageNum);
    return this.readerService.getPageUrl(chapterId, pageNum);
  }

  get CurrentPageBookmarked() {
    return this.bookmarks.hasOwnProperty(this.pageNum);
  }

  get WindowWidth() {
    return this.readingArea?.nativeElement.scrollWidth + 'px';
  }

  get ImageHeight() {
    if (this.FittingOption !== FITTING_OPTION.HEIGHT) {
      return this.mangaReaderService.getPageDimensions(this.pageNum)?.height  + 'px';
    }

    return this.readingArea?.nativeElement?.clientHeight + 'px';
  }

  // This is for the pagination area
  get MaxHeight() {
    return '100dvh';
  }

  get RightPaginationOffset() {
    if (this.readerMode === ReaderMode.LeftRight && this.FittingOption !== FITTING_OPTION.WIDTH) {
      return (this.readingArea?.nativeElement?.scrollLeft || 0) * -1;
    }
    return 0;
  }

  get SplitIconClass() {
    if (this.mangaReaderService.isSplitLeftToRight(this.pageSplitOption)) {
      return 'left-side';
    } else if (this.mangaReaderService.isNoSplit(this.pageSplitOption)) {
      return 'none';
    }
    return 'right-side';
  }

  get FittingOption() { return this.generalSettingsForm?.get('fittingOption')?.value || FITTING_OPTION.HEIGHT; }
  get ReadingAreaWidth() {
    return this.readingArea?.nativeElement.scrollWidth - this.readingArea?.nativeElement.clientWidth;
  }

  get ReadingAreaHeight() {
    return this.readingArea?.nativeElement.scrollHeight - this.readingArea?.nativeElement.clientHeight;
  }


  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');
    if (libraryId === null || seriesId === null || chapterId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.getPageFn = this.getPage.bind(this);

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

      this.generalSettingsForm = this.formBuilder.nonNullable.group({
        autoCloseMenu: new FormControl(this.autoCloseMenu),
        pageSplitOption: new FormControl(this.pageSplitOption),
        fittingOption: new FormControl(this.mangaReaderService.translateScalingOption(this.scalingOption)),
        widthSlider: new FormControl('none'),
        layoutMode: new FormControl(this.layoutMode),
        darkness: new FormControl(100),
        emulateBook: new FormControl(this.user.preferences.emulateBook),
        swipeToPaginate: new FormControl(this.user.preferences.swipeToPaginate)
      });

      this.readerModeSubject.next(this.readerMode);
      this.pagingDirectionSubject.next(this.pagingDirection);

      // We need a mergeMap when page changes
      this.readerSettings$ = merge(this.generalSettingsForm.valueChanges, this.pagingDirection$, this.readerMode$).pipe(
        map(_ => this.createReaderSettingsUpdate()),
        takeUntilDestroyed(this.destroyRef),
      );

      this.updateForm();

      this.pagingDirection$.pipe(
        distinctUntilChanged(),
        tap(dir => {
          this.pagingDirection = dir;
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe(() => {});

      this.readerMode$.pipe(
        distinctUntilChanged(),
        tap(mode => {
          this.readerMode = mode;
          this.disableDoubleRendererIfScreenTooSmall();
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe(() => {});

      this.setupWidthOverrideTriggers();

      this.generalSettingsForm.get('layoutMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {

        const changeOccurred = parseInt(val, 10) !== this.layoutMode;
        this.layoutMode = parseInt(val, 10);

        if (this.layoutMode === LayoutMode.Single) {
          this.generalSettingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption);
          this.generalSettingsForm.get('pageSplitOption')?.enable();
          this.generalSettingsForm.get('widthSlider')?.enable();
          this.generalSettingsForm.get('fittingOption')?.enable();
          this.generalSettingsForm.get('emulateBook')?.enable();
        } else {
          this.generalSettingsForm.get('pageSplitOption')?.setValue(PageSplitOption.NoSplit);
          this.generalSettingsForm.get('pageSplitOption')?.disable();
          this.generalSettingsForm.get('widthSlider')?.disable();
          this.generalSettingsForm.get('fittingOption')?.setValue(this.mangaReaderService.translateScalingOption(ScalingOption.FitToHeight));
          this.generalSettingsForm.get('fittingOption')?.disable();
          this.generalSettingsForm.get('emulateBook')?.enable();
        }
        this.cdRef.markForCheck();

        // Re-render the current page when we switch layouts
        if (changeOccurred) {
          this.setPageNum(this.adjustPagesForDoubleRenderer(this.pageNum));
          this.loadPage();
        }
      });

      this.generalSettingsForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
        this.autoCloseMenu = this.generalSettingsForm.get('autoCloseMenu')?.value;
        this.pageSplitOption = parseInt(this.generalSettingsForm.get('pageSplitOption')?.value, 10);

        const needsSplitting = this.mangaReaderService.isWidePage(this.readerService.imageUrlToPageNum(this.canvasImage.src));
        // If we need to split on a menu change, then we need to re-render.
        if (needsSplitting) {
          // If we need to re-render, to ensure things layout properly, let's update paging direction & reset render
          this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);
          this.canvasRenderer.reset();
          this.loadPage();
        }
      });

      this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(progress => {
        if (!progress) {
          this.toggleMenu();
          this.toastr.info(translate('manga-reader.first-time-reading-manga'));
        }
      });
    });

    this.init();
  }

  ngAfterViewInit() {
    fromEvent(this.readingArea.nativeElement, 'scroll').pipe(debounceTime(20), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.readerMode === ReaderMode.Webtoon) return;
      if (this.readerMode === ReaderMode.LeftRight && this.FittingOption === FITTING_OPTION.HEIGHT) {
        this.rightPaginationOffset = (this.readingArea.nativeElement.scrollLeft) * -1;
        this.cdRef.markForCheck();
        return;
      }
      this.rightPaginationOffset = 0;
      this.cdRef.markForCheck();
    });

    // fromEvent(this.readingArea.nativeElement, 'click').pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe((event: MouseEvent | any) => {
    //   if (event.detail > 1) return;
    //   this.toggleMenu();
    // });

    fromEvent(this.readingArea.nativeElement, 'scroll').pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.prevScrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
      this.prevScrollTop = this.readingArea?.nativeElement?.scrollTop || 0;
      this.hasScrolledX = true;
      this.hasScrolledY = true;
    });
  }

  ngOnDestroy() {
    this.readerService.resetOverrideStyles();
    this.navService.showNavBar();
    this.navService.showSideNav();
    this.showBookmarkEffectEvent.complete();
    if (this.goToPageEvent !== undefined) this.goToPageEvent.complete();
    this.readerService.disableWakeLock();
  }


  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize() {
    this.disableDoubleRendererIfScreenTooSmall();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    switch (this.readerMode) {
      case ReaderMode.LeftRight:
        if (event.key === KEY_CODES.RIGHT_ARROW) {
          if (!this.checkIfPaginationAllowed(KeyDirection.Right)) return;
          this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
        } else if (event.key === KEY_CODES.LEFT_ARROW) {
          if (!this.checkIfPaginationAllowed(KeyDirection.Left)) return;
          this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
        }
        break;
      case ReaderMode.UpDown:
        if (event.key === KEY_CODES.UP_ARROW) {
          if (!this.checkIfPaginationAllowed(KeyDirection.Up)) return;
          this.prevPage();
        } else if (event.key === KEY_CODES.DOWN_ARROW) {
          if (!this.checkIfPaginationAllowed(KeyDirection.Down)) return;
          this.nextPage();
        }
        break;
      case ReaderMode.Webtoon:
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

  /**
   * Width override is only valid under the following conditions:
   *   Image Scaling is Width
   *   Reader Mode is Webtoon
   *
   *   In all other cases, the form will be disabled and set to 0 which indicates default/off state.
   */
  setupWidthOverrideTriggers() {
    const widthOverrideControl = this.generalSettingsForm.get('widthSlider')!;

    const enableWidthOverride = () => {
      widthOverrideControl.enable();
    };

    const disableWidthOverride = () => {
      widthOverrideControl.setValue(0);
      widthOverrideControl.disable();
    };

    const handleControlChanges = () => {
      const fitting = this.generalSettingsForm.get('fittingOption')?.value;
      const splitting = this.generalSettingsForm.get('pageSplitOption')?.value;

      if ((PageSplitOption.FitSplit == splitting && FITTING_OPTION.WIDTH == fitting) || this.readerMode === ReaderMode.Webtoon) {
        enableWidthOverride();
      } else {
        disableWidthOverride();
      }
    };

    // Reader mode changes
    this.readerModeSubject.asObservable()
      .pipe(
        filter(v => v === ReaderMode.Webtoon),
        tap(enableWidthOverride),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();

    // Page split option changes
    this.generalSettingsForm.get('pageSplitOption')?.valueChanges.pipe(
      distinctUntilChanged(),
      tap(handleControlChanges),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    // Fitting option changes
    this.generalSettingsForm.get('fittingOption')?.valueChanges.pipe(
      tap(handleControlChanges),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    // Set the default override to 0
    widthOverrideControl.setValue(0);

    //send the current width override value to the label
    this.widthOverrideLabel$ = this.readerSettings$?.pipe(
      map(values => (parseInt(values.widthSlider) <= 0) ? '' : values.widthSlider + '%'),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  createReaderSettingsUpdate() {
    return {
      pageSplit: parseInt(this.generalSettingsForm.get('pageSplitOption')?.value, 10),
      fitting: (this.generalSettingsForm.get('fittingOption')?.value as FITTING_OPTION),
      widthSlider: this.generalSettingsForm.get('widthSlider')?.value,
      layoutMode: this.layoutMode,
      darkness: parseInt(this.generalSettingsForm.get('darkness')?.value + '', 10) || 100,
      pagingDirection: this.pagingDirection,
      readerMode: this.readerMode,
      emulateBook: this.generalSettingsForm.get('emulateBook')?.value,
    };
  }

  // If we are in double mode, we need to check if our current page is on a right edge or not, if so adjust by decrementing by 1
  adjustPagesForDoubleRenderer(pageNum: number) {
    if (pageNum === this.maxPages - 1) return pageNum;
    if (this.readerMode !== ReaderMode.Webtoon && this.layoutMode !== LayoutMode.Single) {
      return this.mangaReaderService.adjustForDoubleReader(pageNum);
    }
    return pageNum;
  }

  switchToWebtoonReaderIfPagesLikelyWebtoon() {
    if (this.readerMode === ReaderMode.Webtoon) return;

    if (this.mangaReaderService.shouldBeWebtoonMode()) {
      this.readerMode = ReaderMode.Webtoon;
      this.toastr.info(translate('toasts.webtoon-override'));
      this.readerModeSubject.next(this.readerMode);
      this.cdRef.markForCheck();
    }
  }

  disableDoubleRendererIfScreenTooSmall() {
    if (window.innerWidth > window.innerHeight) {
      this.generalSettingsForm.get('layoutMode')?.enable();
      this.cdRef.markForCheck();
      return;
    }
    if (this.layoutMode === LayoutMode.Single || this.readerMode === ReaderMode.Webtoon) return;

    this.generalSettingsForm.get('layoutMode')?.setValue(LayoutMode.Single);
    this.generalSettingsForm.get('layoutMode')?.disable();
    this.toastr.info(translate('manga-reader.layout-mode-switched'));
    this.cdRef.markForCheck();
  }

  /**
   * Gets a page from cache else gets a brand new Image
   * @param pageNum Page Number to load
   * @param forceNew Forces to fetch a new image
   * @param chapterId ChapterId to fetch page from. Defaults to current chapterId. Not used when in bookmark mode
   * @returns HTMLImageElement | undefined
   */
   getPage(pageNum: number, chapterId: number = this.chapterId, forceNew: boolean = false) {

    let img: HTMLImageElement | undefined;
    if (this.bookmarkMode) img = this.cachedImages.find(img => this.readerService.imageUrlToPageNum(img.src) === pageNum);
    else img = this.cachedImages.find(img => this.readerService.imageUrlToPageNum(img.src) === pageNum
      && (this.readerService.imageUrlToChapterId(img.src) == chapterId || this.readerService.imageUrlToChapterId(img.src) === -1)
    );

    //console.log('Requesting page ', pageNum, ' found page: ', img, ' and app is requesting new image? ', forceNew);
    if (!img || forceNew) {
      img = new Image();
      img.src = this.getPageUrl(pageNum, chapterId);
      img.onload = (evt) => {
        this.currentImage.next(img!);
        this.cdRef.markForCheck();
      }
      // img.onerror = (evt) => {
      //   const event = evt as Event;
      //   const page = this.readerService.imageUrlToPageNum((event.target as HTMLImageElement).src);
      //   console.error('Image failed to load: ', page);
      //   (event.target as HTMLImageElement).onerror = null;
      //   const newSrc = this.getPageUrl(pageNum, chapterId) + '#' + new Date().getTime();
      //   console.log('requesting page ', page, ' with url: ', newSrc);
      //   (event.target as HTMLImageElement).src = newSrc;
      //   this.cdRef.markForCheck();
      // }
    }

    return img;
  }



  isHorizontalScrollLeft() {
    const scrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
    // if scrollLeft is 0 and this.ReadingAreaWidth is 0, then there is no scroll needed
    // if they equal each other, it means we are at the end of the scroll area
    if (scrollLeft === 0 && this.ReadingAreaWidth === 0) return false;
    if (scrollLeft === this.ReadingAreaWidth) return false;
    return scrollLeft < this.ReadingAreaWidth;
  }

  isVerticalScrollLeft() {
    const scrollTop = this.readingArea?.nativeElement?.scrollTop || 0;
    return scrollTop < this.ReadingAreaHeight;
  }

  /**
   * Is there any room to scroll in the direction we are giving? If so, return false. Otherwise, return true.
   * @param direction
   * @returns
   */
  checkIfPaginationAllowed(direction: KeyDirection) {
    if (this.readingArea === undefined || this.readingArea.nativeElement === undefined) return true;

    const scrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
    const scrollTop = this.readingArea?.nativeElement?.scrollTop || 0;

    switch (direction) {
      case KeyDirection.Right:
        if (this.prevIsHorizontalScrollLeft && !this.isHorizontalScrollLeft()) { return true; }
        this.prevIsHorizontalScrollLeft = this.isHorizontalScrollLeft();

        if (this.isHorizontalScrollLeft()) {
          return false;
        }
        break;
      case KeyDirection.Left:
        this.prevIsHorizontalScrollLeft = this.isHorizontalScrollLeft();
        if (scrollLeft > 0 || this.prevScrollLeft > 0) {
          return false;
        }
        break;
      case KeyDirection.Up:
        this.prevIsVerticalScrollLeft = this.isVerticalScrollLeft();
        if (scrollTop > 0|| this.prevScrollTop > 0) {
          return false;
        }
        break;
      case KeyDirection.Down:
        if (this.prevIsVerticalScrollLeft && !this.isVerticalScrollLeft()) { return true; }
        this.prevIsVerticalScrollLeft = this.isVerticalScrollLeft();

        if (this.isVerticalScrollLeft()) {
          return false;
        }
        break;
    }

    return true;
  }



  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.pageNum = 0;
    this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);
    this.inSetup = true;
    this.canvasImage.src = '';
    this.cdRef.markForCheck();

    this.cachedImages = [];
    for (let i = 0; i < PREFETCH_PAGES; i++) {
      this.cachedImages.push(new Image());
    }

    if (this.goToPageEvent) {
      // There was a bug where goToPage was emitting old values into infinite scroller between chapter loads. We explicitly clear it out between loads,
      // and we use a BehaviourSubject to ensure only latest value is sent
      this.goToPageEvent.complete();
    }

    if (this.bookmarkMode) {
      this.readerService.getBookmarkInfo(this.seriesId).subscribe(bookmarkInfo => {
        this.setPageNum(0);
        this.title = bookmarkInfo.seriesName;
        this.subtitle = translate('manga-reader.bookmarks-title');
        this.libraryType = bookmarkInfo.libraryType;
        this.maxPages = bookmarkInfo.pages;
        this.mangaReaderService.load(bookmarkInfo);

        // Due to change detection rules in Angular, we need to re-create the options object to apply the change
        const newOptions: Options = Object.assign({}, this.pageOptions);
        newOptions.ceil = this.maxPages - 1; // We -1 so that the slider UI shows us hitting the end, since visually we +1 everything.
        this.pageOptions = newOptions;
        this.inSetup = false;
        this.cdRef.markForCheck();

        this.goToPageEvent = new BehaviorSubject<number>(this.pageNum);

        this.render();
      });

      setTimeout(() => {
        this.readerService.enableWakeLock(this.reader.nativeElement);
      }, 1000);
      return;
    }

    forkJoin({
      progress: this.readerService.getProgress(this.chapterId),
      chapterInfo: this.readerService.getChapterInfo(this.chapterId, true),
      bookmarks: this.readerService.getBookmarks(this.chapterId),
    }).pipe(take(1)).subscribe(results => {
      if (this.readingListMode && (results.chapterInfo.seriesFormat === MangaFormat.EPUB || results.chapterInfo.seriesFormat === MangaFormat.PDF)) {
        // Redirect to the book reader.
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(this.readerService.getNavigationArray(results.chapterInfo.libraryId, results.chapterInfo.seriesId, this.chapterId, results.chapterInfo.seriesFormat), {queryParams: params});
        return;
      }

      this.mangaReaderService.load(results.chapterInfo);

      this.continuousChapterInfos[ChapterInfoPosition.Current] = results.chapterInfo;
      this.volumeId = results.chapterInfo.volumeId;
      this.maxPages = results.chapterInfo.pages;
      let page = results.progress.pageNum;
      if (page > this.maxPages) {
        page = this.maxPages - 1;
      }

      page = this.adjustPagesForDoubleRenderer(page);

      this.totalSeriesPages = results.chapterInfo.seriesTotalPages;
      this.totalSeriesPagesRead = results.chapterInfo.seriesTotalPagesRead - page;

      this.setPageNum(page); // first call
      this.goToPageEvent = new BehaviorSubject<number>(this.pageNum);

      // Due to change detection rules in Angular, we need to re-create the options object to apply the change
      const newOptions: Options = Object.assign({}, this.pageOptions);
      newOptions.ceil = this.maxPages - 1; // We -1 so that the slider UI shows us hitting the end, since visually we +1 everything.
      this.pageOptions = newOptions;

      this.libraryType = results.chapterInfo.libraryType;
      this.title = results.chapterInfo.title;
      this.subtitle = results.chapterInfo.subtitle;

      this.inSetup = false;

      this.disableDoubleRendererIfScreenTooSmall();
      this.switchToWebtoonReaderIfPagesLikelyWebtoon();


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
        } else {
          // Fetch the first page of next chapter
          this.getPage(0, this.nextChapterId);

        }
      });
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
          this.prevChapterDisabled = true;
          this.cdRef.markForCheck();
        } else {
          // Fetch the last page of prev chapter
          this.getPage(1000000, this.prevChapterId);
        }
      });

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

  cancelMenuCloseTimer() {
    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
    }
  }

  /**
   * Whenever the menu is interacted with, restart the timer. However, if the settings menu is open, don't restart, just cancel the timeout.
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

  resetSwipeModifiers() {
    this.prevScrollLeft = 0;
    this.prevScrollTop = 0;
    this.hasScrolledX = false;
    this.hasScrolledY = false;
    this.hasHitRightScroll = false;
    this.hasHitZeroScroll = false;
    this.hasHitBottomTopScroll = false;
    this.hasHitZeroTopScroll = false;
  }

  /**
   * This executes BEFORE fromEvent('scroll')
   * @returns
   * @param _
   */
  onSwipeMove(_: SwipeEvent) {
    this.prevScrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
    this.prevScrollTop = this.readingArea?.nativeElement?.scrollTop || 0
  }

  triggerSwipePagination(direction: KeyDirection) {
    if (!this.generalSettingsForm.get('swipeToPaginate')?.value) return;

    switch(direction) {
      case KeyDirection.Down:
        this.nextPage();
        break;
      case KeyDirection.Right:
        this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
        break;
      case KeyDirection.Up:
        this.prevPage();
        break;
      case KeyDirection.Left:
          this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
          break;
    }

  }

  onSwipeEnd(event: SwipeEvent) {
    // Positive number means swiping right/down, negative means left
    switch (this.readerMode) {
      case ReaderMode.Webtoon: break;
      case ReaderMode.LeftRight:
        {
          if (event.direction !== 'x') return;
          const scrollLeft = this.readingArea?.nativeElement?.scrollLeft || 0;
          const direction = event.distance < 0 ? KeyDirection.Right : KeyDirection.Left;
          if (!this.checkIfPaginationAllowed(direction)) {
            return;
          }


          // We just came from a swipe where pagination was required, and we are now at the end of the swipe, so make the user do it once more
          if (direction === KeyDirection.Right) {
            this.hasHitZeroScroll = false;
            if (scrollLeft === 0 && this.ReadingAreaWidth === 0) {
              this.triggerSwipePagination(direction);
              return;
            }
            if (!this.hasHitRightScroll && this.checkIfPaginationAllowed(direction)) {
              this.hasHitRightScroll = true;
              return;
            }
          } else if (direction === KeyDirection.Left) {
            this.hasHitRightScroll = false;

            // If we have not scrolled then let the user page back
            if (scrollLeft === 0 && this.prevScrollLeft === 0) {
              if (!this.hasScrolledX || this.hasHitZeroScroll) {
                this.triggerSwipePagination(direction);
                return;
              }
              this.hasHitZeroScroll = true;
              return;
            }
          }

          if (!this.hasHitRightScroll) {
            return;
          }

          this.triggerSwipePagination(direction);
          break;
        }
      case ReaderMode.UpDown:
        {
          if (event.direction !== 'y') return;
          const direction = event.distance < 0 ? KeyDirection.Down : KeyDirection.Up;
          const scrollTop = this.readingArea?.nativeElement?.scrollTop || 0;
          if (!this.checkIfPaginationAllowed(direction)) return;


          if (direction === KeyDirection.Down) {
            this.hasHitZeroTopScroll = false;
            if (!this.hasHitBottomTopScroll && this.checkIfPaginationAllowed(direction)) {
              this.hasHitBottomTopScroll = true;
              return;
            }
          } else if (direction === KeyDirection.Up) {
            this.hasHitBottomTopScroll = false;

            // If we have not scrolled then let the user page back
            if (scrollTop === 0 && this.prevScrollTop === 0) {
              if (!this.hasScrolledY || this.hasHitZeroTopScroll) {
                this.triggerSwipePagination(direction);
                return;
              }
              this.hasHitZeroTopScroll = true;
              return;
            }
          }

          if (!this.hasHitBottomTopScroll) {
            return;
          }

          this.triggerSwipePagination(direction);
          break;
        }
    }
  }

  handlePageChange(event: any, direction: KeyDirection) {
    if (this.readerMode === ReaderMode.Webtoon) {
      if (direction === KeyDirection.Right) {
        this.nextPage(event);
      } else {
        this.prevPage(event);
      }
      return;
    }
    if (direction === KeyDirection.Right) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage(event) : this.prevPage(event);
    } else if (direction === KeyDirection.Left) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage(event) : this.nextPage(event);
    }
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    this.resetSwipeModifiers();

    this.isLoading = true;
    this.cdRef.markForCheck();

    this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);

    const pageAmount = Math.max(this.canvasRenderer.getPageAmount(PAGING_DIRECTION.FORWARD), this.singleRenderer.getPageAmount(PAGING_DIRECTION.FORWARD),
                                this.doubleRenderer.getPageAmount(PAGING_DIRECTION.FORWARD),
                                this.doubleReverseRenderer.getPageAmount(PAGING_DIRECTION.FORWARD),
                                this.doubleNoCoverRenderer.getPageAmount(PAGING_DIRECTION.FORWARD)
                              );
    // If we are on last page with split mode, we need to be able to progress, hence why we check if we could move backwards or not
    const isSplitRendering = [PageSplitOption.SplitRightToLeft, PageSplitOption.SplitRightToLeft].includes(parseInt(this.generalSettingsForm.get('pageSplitOption')?.value, 10));
    const notInSplit = this.canvasRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS) === 0;
    const isASpread = this.mangaReaderService.isWidePage(this.pageNum);


    if ((this.pageNum + pageAmount >= this.maxPages && (!isASpread || !isSplitRendering || notInSplit))) {
      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }

    this.setPageNum(this.pageNum + pageAmount);
    this.loadPage();
  }

  prevPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    this.resetSwipeModifiers();

    this.isLoading = true;
    this.cdRef.markForCheck();

    this.pagingDirectionSubject.next(PAGING_DIRECTION.BACKWARDS);


    const pageAmount = this.readerMode === ReaderMode.Webtoon ? 1 : Math.max(this.canvasRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS),
                                this.singleRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS),
                                this.doubleRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS),
                                this.doubleNoCoverRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS),
                                this.doubleReverseRenderer.getPageAmount(PAGING_DIRECTION.BACKWARDS)
                              );

    const notInSplit = this.readerMode === ReaderMode.Webtoon ? true : this.canvasRenderer.shouldMovePrev();

    if ((this.pageNum - 1 < 0 && notInSplit)) {
      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    this.setPageNum(this.pageNum - pageAmount);
    this.loadPage();
  }

  /**
   * Sets canvasImage's src to current page, but first attempts to use a pre-fetched image
   */
  setCanvasImage() {
    if (this.cachedImages === undefined) return;

    this.canvasImage = this.getPage(this.pageNum, this.chapterId, this.layoutMode !== LayoutMode.Single);
    if (!this.canvasImage.complete) {
      this.canvasImage.addEventListener('load', () => {
        this.currentImage.next(this.canvasImage);
      }, false);
    } else {
      this.currentImage.next(this.canvasImage);
    }


    this.cdRef.markForCheck();
  }


  loadNextChapter() {
    if (this.nextPageDisabled || this.nextChapterDisabled || this.bookmarkMode) {
      this.toastr.info(translate('manga-reader.no-next-chapter'));
      this.isLoading = false;
      this.cdRef.markForCheck();
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
      this.toastr.info(translate('manga-reader.no-prev-chapter'));
      this.isLoading = false;
      this.cdRef.markForCheck();
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
      const msg = translate(direction === 'Next' ? 'toasts.load-next-chapter' : 'toasts.load-prev-chapter', {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
      this.toastr.info(msg, '', {timeOut: 3000});
    } else {
      // This will only happen if no actual chapter can be found
      const msg = translate(direction === 'Next' ? 'toasts.no-next-chapter' : 'toasts.no-prev-chapter',
        {entity: this.utilityService.formatChapterName(this.libraryType).toLowerCase()});
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



  renderPage() {
    const page = [this.canvasImage];

    this.canvasRenderer?.renderPage(page);
    this.singleRenderer?.renderPage(page);
    this.doubleRenderer?.renderPage(page);
    this.doubleNoCoverRenderer?.renderPage(page);
    this.doubleReverseRenderer?.renderPage(page);

    // Originally this was only for fit to height, but when swiping was introduced, it made more sense to do it always to reset to the same view
    this.readingArea.nativeElement.scroll(0,0);

    this.isLoading = false;
    this.cdRef.markForCheck();
  }

  /**
   * Maintains an array of images (that are requested from backend) around the user's current page. This allows for quick loading (seamless to user)
   * and also maintains page info (wide image, etc.) due to onload event.
   */
  prefetch() {
    // NOTE: This doesn't allow for any directionality
    // NOTE: This doesn't maintain 1 image behind at all times
    for(let i = 0; i <= PREFETCH_PAGES - 3; i++) {
      let numOffset = this.pageNum + i;

      if (numOffset > this.maxPages - 1) {
        break;
      }

      const index = (numOffset % this.cachedImages.length + this.cachedImages.length) % this.cachedImages.length;
      const cachedImagePageNum = this.readerService.imageUrlToPageNum(this.cachedImages[index].src);
      if (cachedImagePageNum !== numOffset) {
        this.cachedImages[index] = this.getPage(numOffset, this.chapterId);
      }
    }
  }


  /**
   * This is responsible for setting up the image variables. This will be moved out to different renderers
   */
  loadPage() {
    if (this.readerMode === ReaderMode.Webtoon) return;

    this.isLoading = true;
    this.setCanvasImage();
    this.cdRef.markForCheck();

    this.renderPage();
    this.isLoading = false;
    this.cdRef.markForCheck();

    this.prefetch();
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }

    if (this.menuOpen && this.user.preferences.showScreenHints) {
      this.showClickOverlay = true;
      this.showClickOverlaySubject.next(true);
      setTimeout(() => {
        this.showClickOverlay = false;
        this.showClickOverlaySubject.next(false);
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
      this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);
    } else {
      this.pagingDirectionSubject.next(PAGING_DIRECTION.BACKWARDS);
    }

    this.setPageNum(this.adjustPagesForDoubleRenderer(page));
    this.refreshSlider.emit();
    this.goToPageEvent.next(this.pageNum);
    this.render();
  }

  setPageNum(pageNum: number) {
    this.pageNum = Math.max(Math.min(pageNum, this.maxPages - 1), 0);
    this.pageNumSubject.next({pageNum: this.pageNum, maxPages: this.maxPages});
    this.cdRef.markForCheck();

    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (this.nextChapterId > 0 && !this.nextChapterPrefetched) {
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1)).subscribe(res => {
          this.continuousChapterInfos[ChapterInfoPosition.Next] = res;
          this.nextChapterPrefetched = true;
          this.prefetchStartOfChapter(this.nextChapterId, PAGING_DIRECTION.FORWARD);
        });
      }
    } else if (this.pageNum <= 10) {
      if (this.prevChapterId > 0 && !this.prevChapterPrefetched) {
        this.readerService.getChapterInfo(this.prevChapterId).pipe(take(1)).subscribe(res => {
          this.continuousChapterInfos[ChapterInfoPosition.Previous] = res;
          this.prevChapterPrefetched = true;
          this.prefetchStartOfChapter(this.nextChapterId, PAGING_DIRECTION.BACKWARDS);
        });
      }
    }

    // Due to the fact that we start at image 0, but page 1, we need the last page to have progress as page + 1 to be completed
    let tempPageNum = this.pageNum;
    if (this.pageNum == this.maxPages - 1 && this.pagingDirection === PAGING_DIRECTION.FORWARD) {
      tempPageNum = this.pageNum + 1;
    }

    if (!this.incognitoMode && !this.bookmarkMode) {
      this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, this.chapterId, tempPageNum).pipe(take(1)).subscribe(() => {/* No operation */});
    }
  }

  /**
   * Loads the first 5 images (throwaway cache) from the given chapterId
   * @param chapterId
   * @param direction Used to indicate if the chapter is behind or ahead of current chapter
   */
  prefetchStartOfChapter(chapterId: number, direction: PAGING_DIRECTION) {
    let pages = [];

    if (direction === PAGING_DIRECTION.BACKWARDS) {
      if (this.continuousChapterInfos[ChapterInfoPosition.Previous] === undefined) return;
      const n = this.continuousChapterInfos[ChapterInfoPosition.Previous]!.pages;
      // Ensure we only load up to 5 pages backward
      pages = Array.from({ length: Math.min(n + 1, 5) }, (v, k) => n - k);
    } else {
      pages = [0, 1, 2, 3, 4];
    }

    const images = [];
    pages.forEach((_, i: number) => {
      const img = new Image();
      img.src = this.getPageUrl(i, chapterId);
      images.push(img)
    });
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
      this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);
    } else {
      this.pagingDirectionSubject.next(PAGING_DIRECTION.BACKWARDS);
    }

    this.setPageNum(this.adjustPagesForDoubleRenderer(page));
    this.goToPageEvent.next(page);
    this.render();
  }

  // This is menu code
  clickOverlayClass(side: 'right' | 'left') {
    if (!this.showClickOverlay) {
      return '';
    }

    if (this.readingDirection === ReadingDirection.LeftToRight) {
      return side === 'right' ? 'highlight' : 'highlight-2';
    }
    return side === 'right' ? 'highlight-2' : 'highlight';
  }

  // This is menu only code
  promptForPage() {
    const question = translate('book-reader.go-to-page-prompt', {totalPages: this.maxPages});
    const goToPageNum = window.prompt(question, '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  // This is menu only code
  toggleFullscreen() {
      this.readerService.toggleFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = true;
        this.fullscreenEvent.next(true);
        this.render();
      });
  }

  // This is menu only code
  toggleReaderMode() {
    switch(this.readerMode) {
      case ReaderMode.LeftRight:
        this.pagingDirectionSubject.next(PAGING_DIRECTION.FORWARD);
        this.readerModeSubject.next(ReaderMode.UpDown);
        break;
      case ReaderMode.UpDown:
        this.readerModeSubject.next(ReaderMode.Webtoon);
        break;
      case ReaderMode.Webtoon:
        this.readerModeSubject.next(ReaderMode.LeftRight);
        break;
    }

    // We must set this here because loadPage from render doesn't call if we aren't page splitting
    if (this.readerMode !== ReaderMode.Webtoon) {
      this.canvasImage = this.getPage(this.pageNum);
      this.currentImage.next(this.canvasImage);
      this.pageNumSubject.next({pageNum: this.pageNum, maxPages: this.maxPages});
      //this.isLoading = true;
      this.cdRef.detectChanges(); // Must use detectChanges to ensure ViewChildren get updated again
    }

    this.updateForm();

    this.render();
  }

  // This is menu only code
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
  bookmarkPage(event: Event | undefined = undefined) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.bookmarkMode) return;
    if (!(this.accountService.hasBookmarkRole(this.user) || this.accountService.hasAdminRole(this.user))) return;

    const pageNum = this.pageNum;
    // if canvasRenderer and doubleRenderer is undefined, then we are in webtoon mode
    const isDouble = this.canvasRenderer !== undefined && this.doubleRenderer !== undefined && Math.max(this.canvasRenderer.getBookmarkPageCount(), this.singleRenderer.getBookmarkPageCount(),
      this.doubleRenderer.getBookmarkPageCount(), this.doubleReverseRenderer.getBookmarkPageCount(), this.doubleNoCoverRenderer.getBookmarkPageCount()) > 1;

    if (this.CurrentPageBookmarked) {
      let apis = [this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, pageNum)];
      if (isDouble) apis.push(this.readerService.unbookmark(this.seriesId, this.volumeId, this.chapterId, pageNum + 1));
      forkJoin(apis).pipe(take(1)).subscribe(() => {
        delete this.bookmarks[pageNum];
        if (isDouble) delete this.bookmarks[pageNum + 1];
        this.cdRef.detectChanges();
      });
    } else {
      let apis = [this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum)];
      if (isDouble) apis.push(this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum + 1));
      forkJoin(apis).pipe(take(1)).subscribe(() => {
        this.bookmarks[pageNum] = 1;
        if (isDouble) this.bookmarks[pageNum + 1] = 1;
        this.cdRef.detectChanges();
      });
    }

    // Show an effect on the image to show that it was bookmarked
    this.showBookmarkEffectEvent.next(pageNum);
  }

  // This is menu only code
  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
  turnOffIncognito() {
    this.incognitoMode = false;
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info(translate('toasts.incognito-off'));
    if (!this.bookmarkMode) {
      this.readerService.saveProgress(this.libraryId, this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});
    }
  }

  // This is menu only code
  openShortcutModal() {
    const ref = this.modalService.open(ShortcutsModalComponent, { scrollable: true, size: 'md' });
    ref.componentInstance.shortcuts = [
      {key: '⇽', description: 'prev-page'},
      {key: '⇾', description: 'next-page'},
      {key: '↑', description: 'prev-page'},
      {key: '↓', description: 'next-page'},
      {key: 'G', description: 'go-to'},
      {key: 'B', description: 'bookmark'},
      {key: translate('shortcuts-modal.double-click'), description: 'bookmark'},
      {key: 'ESC', description: 'close-reader'},
      {key: 'SPACE', description: 'toggle-menu'},
    ];
  }

  // menu only code
  savePref() {
    const modelSettings = this.generalSettingsForm.getRawValue();
    // Get latest preferences from user, overwrite with what we manage in this UI, then save
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) return;
      const data = {...user.preferences};
      data.layoutMode = parseInt(modelSettings.layoutMode, 10);
      data.readerMode = this.readerMode;
      data.autoCloseMenu = this.autoCloseMenu;
      data.readingDirection = this.readingDirection;
      data.emulateBook = modelSettings.emulateBook;
      data.swipeToPaginate = modelSettings.swipeToPaginate;
      data.pageSplitOption = parseInt(modelSettings.pageSplitOption, 10);
      data.locale = data.locale || 'en';

      this.accountService.updatePreferences(data).subscribe(updatedPrefs => {
        this.toastr.success(translate('manga-reader.user-preferences-updated'));
        if (this.user) {
          this.user.preferences = updatedPrefs;
          this.cdRef.markForCheck();
        }
      })
    });
  }

  translatePrefOptions(o: {text: string, value: any}) {
    const d = {...o};
    d.text = translate('preferences.' + o.text);
    return d;
  }

}
