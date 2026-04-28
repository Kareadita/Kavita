import {AsyncPipe, DOCUMENT} from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  output,
  Renderer2,
  signal,
  Signal,
  SimpleChanges,
  viewChild
} from '@angular/core';
import {BehaviorSubject, fromEvent, map, Observable, of, ReplaySubject, Subject, tap} from 'rxjs';
import {debounceTime, distinctUntilChanged} from 'rxjs/operators';
import {ReaderService} from '../../../_services/reader.service';
import {PAGING_DIRECTION} from '../../_models/reader-enums';
import {WebtoonImage} from '../../_models/webtoon-image';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {InfiniteScrollDirective} from "ngx-infinite-scroll";
import {ReaderSetting} from "../../_models/reader-setting";
import {SafeStylePipe} from "../../../_pipes/safe-style.pipe";
import {ReadingProfile} from "../../../_models/preferences/reading-profiles";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {Queue} from "../../../shared/data-structures/queue";
import {PullState, PullToLoadComponent} from "../../../shared/_components/pull-to-load/pull-to-load.component";

/**
 * Default debounce time from scroll and scrollend event listeners
 */
const DEFAULT_SCROLL_DEBOUNCE = 20;
/**
 * Safari does not support the scrollEnd event, we can use scroll event with higher debounce time to emulate it
 */
const EMULATE_SCROLL_END_DEBOUNCE = 100;
/**
 * Time which must have passed before auto chapter changes can occur.
 * See: https://github.com/Kareadita/Kavita/issues/3970
 */
const INITIAL_LOAD_GRACE_PERIOD = 1000;
/**
 * How many times the Webtoon reader will retry failed images
 */
const MAX_FAILED_IMG_RETRIES = 3;
/**
 * How long to wait for an image load/error event before treating it as a failure
 */
const IMAGE_RETRY_TIMEOUT_MS = 10_000;
/** Time to wait between progress events **/
const PROGRESS_SAVE_TIMEOUT_MS = 200;
/**
 * Bitwise enums for configuring how much debug information we want
 */
const enum DEBUG_MODES {
  /**
   * No Debug information
   */
  None = 0,
  /**
   * Turn on debug logging
   */
  Logs = 2,
  /**
   * Turn on the action bar in UI
   */
  ActionBar = 4,
  /**
   * Turn on Page outline
   */
  Outline = 8
}

@Component({
    selector: 'app-infinite-scroller',
    templateUrl: './infinite-scroller.component.html',
    styleUrls: ['./infinite-scroller.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AsyncPipe, TranslocoDirective, InfiniteScrollDirective, SafeStylePipe, PullToLoadComponent]
})
export class InfiniteScrollerComponent implements OnInit, OnChanges, OnDestroy, AfterViewInit {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly mangaReaderService = inject(MangaReaderService);
  private readonly readerService = inject(ReaderService);
  private readonly renderer = inject(Renderer2);
  private readonly injector = inject(Injector);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly breakpointService = inject(BreakpointService);

  scrollContainer = viewChild.required<ElementRef<HTMLDivElement>>('scroller');
  pullToLoadNext = viewChild<PullToLoadComponent>('pullToLoadNext');
  ignoreNextScrollEvent = signal(false);

  get scrollElement(): HTMLElement {
    return this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body;
  }

  /**
   * Current page number aka what's recorded on screen
   */
  @Input() pageNum: number = 0;
  /**
   * Number of pages to prefetch ahead of position
   */
  @Input() bufferPages: number = 5;
  /**
   * Total number of pages
   */
  @Input() totalPages: number = 0;
  /**
   * Method to generate the src for Image loading
   */
  @Input({required: true}) urlProvider!: (page: number) => string;
  @Input({required: true}) readerSettings$!: Observable<ReaderSetting>;
  @Input({required: true}) readingProfile!: ReadingProfile;
  @Input({required: true}) chapterId!: number;

  readonly pageNumberChange = output<number>();
  readonly loadNextChapter = output<void>();
  readonly loadPrevChapter = output<void>();

  @Input() goToPage: BehaviorSubject<number> | undefined;
  @Input() bookmarkPage: ReplaySubject<number> = new ReplaySubject<number>();
  @Input() fullscreenToggled: ReplaySubject<boolean> = new ReplaySubject<boolean>();

  darkness$: Observable<string> = of('brightness(100%)');

  readerElemRef!: ElementRef<HTMLDivElement>;
  /** This will update the output to allow for throttling, since we hit the page change on scroll event **/
  private pageChangeSubject = new Subject<number>();

  /**
   * Stores and emits all the src urls
   */
  webtoonImages: BehaviorSubject<WebtoonImage[]> = new BehaviorSubject<WebtoonImage[]>([]);
  /** Urls that need to be retried for download **/
  retryImages = new Queue<{page: number, src: string, chapterId: number, retryCount: number}>();
  isProcessingRetries = false;

  /**
   * Responsible for calculating current page on screen and uses hooks to trigger prefetching.
   * Note: threshold will fire differently due to size of images. 1 requires full image on screen. 0 means 1px on screen. We use 0.01 as 0 does not work currently.
   */
  intersectionObserver: IntersectionObserver = new IntersectionObserver((entries) => this.handleIntersection(entries), { threshold: 0.01 });
  /**
   * Direction we are scrolling. Controls calculations for prefetching
   */
  scrollingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;
  /**
   * Temp variable to keep track of scrolling position between scrolls to caclulate direction
   */
  prevScrollPosition: number = 0;
  /**
   * Temp variable to keep track of when the scrollTo() finishes, so we can start capturing scroll events again
   */
  currentPageElem: Element | null = null;
  /**
   * The minimum width of images in webtoon. On image loading, this is checked and updated. All images will get this assigned to them for rendering.
   */
  webtoonImageWidth: number = window.innerWidth || this.document.body.clientWidth || this.document.documentElement.clientWidth;
  /**
   * Used to tell if a scrollTo() operation is in progress
   */
  isScrolling: boolean = false;
  /**
   * Whether all prefetched images have loaded on the screen (not neccesarily in viewport)
   */
  allImagesLoaded: boolean = false;
  /**
   * Denotes each page that has been loaded or not. If pruning is implemented, the key will be deleted.
   */
   imagesLoaded: {[key: number]: number} = {};
  /**
   * If the user has scrolled all the way to the bottom. This is used solely for continuous reading
   */
   atBottom: boolean = false;
   /**
   * If the user has scrolled all the way to the top. This is used solely for continuous reading
   */
   atTop: boolean = false;
   /**
    * If the manga reader is in fullscreen. Some math changes based on this value.
    */
   isFullscreenMode: boolean = false;
   /**
    * Tracks the first load, until all the initial prefetched images are loaded. We use this to reduce opacity so images can load without jerk.
    */
   initFinished: boolean = false;
  /**
   * True until INITIAL_LOAD_GRACE_PERIOD ms have passed since the component was created
   */
  isInitialLoad = true;
  /**
   * Debug mode. Will show extra information. Use bitwise (|) operators between different modes to enable different output
   */
  debugMode: DEBUG_MODES = DEBUG_MODES.None;
  /**
   * Debug mode. Will filter out any messages in here so they don't hit the log
   */
  debugLogFilter: Array<string> = ['[PREFETCH]', '[Intersection]', '[Visibility]', '[Image Load]'];

  readerSettings!: Signal<ReaderSetting>;
  widthOverride!: Signal<string>;

  get minPageLoaded() {
    return Math.min(...Object.values(this.imagesLoaded));
  }

  get maxPageLoaded() {
    return Math.max(...Object.values(this.imagesLoaded));
  }

  get areImagesWiderThanWindow() {
    let [_, innerWidth] = this.getInnerDimensions();
    return this.webtoonImageWidth > (innerWidth || document.body.clientWidth);
  }

  constructor() {
    const document = this.document;

    // This will always exist at this point in time since this is used within manga reader
    const reader = document.querySelector('.reading-area');
    if (reader !== null) {
      this.readerElemRef = new ElementRef(reader as HTMLDivElement);
    }

    this.pageChangeSubject.pipe(
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
      tap(page => this.pageNumberChange.emit(page)),
    ).subscribe();

    let previousState: PullState = PullState.Idle;
    effect(() => {
      const pullToLoad = this.pullToLoadNext();
      if (!pullToLoad) return;

      const currentState = pullToLoad.state();

      // On mobile devices with a sufficiently small last image, the debounce from moving into idle
      // causes the scroll event to fire with a wrong page number. We ignore one scroll event to prevent this from
      // happening.
      if (previousState === PullState.Triggered && currentState === PullState.Idle) {
        this.debugLog('Ignoring next scroll event to compensate for PullToLoad debounce')
        this.ignoreNextScrollEvent.set(true);
      }

      previousState = currentState;
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes.hasOwnProperty('totalPages') && changes['totalPages'].previousValue != changes['totalPages'].currentValue) {
      this.totalPages = changes['totalPages'].currentValue;
      this.cdRef.markForCheck();
      this.initWebtoonReader();
    }
  }

  ngOnDestroy(): void {
    this.intersectionObserver.disconnect();
  }

  ngAfterViewInit() {
    this.scrollContainer().nativeElement.focus();
  }

  /**
   * Responsible for binding the scroll handler to the correct event. On non-fullscreen, body is correct. However, on fullscreen, we must use the reader as that is what
   * gets promoted to fullscreen.
   */
  initScrollHandler() {
    const element = this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body;

    // Reset any modal-induced overflow lock (this can happen when Starting Over and ngBootstrap modal hasn't completed teardown)
    if (element === this.document.body) {
      setTimeout(() => {
        this.document.body.style.overflow = 'auto';
        this.document.body.classList.remove('modal-open'); // ngBootstrap adds this
      }, 100);
    }

    fromEvent(element, 'scroll')
      .pipe(
        debounceTime(DEFAULT_SCROLL_DEBOUNCE),
        takeUntilDestroyed(this.destroyRef),
        tap((event) => this.handleScrollEvent(event))
      )
      .subscribe();

    const isScrollEndSupported = 'onscrollend' in document;
    const scrollEndEvent = isScrollEndSupported ? 'scrollend' : 'scroll';
    const scrollEndDebounce = isScrollEndSupported ? DEFAULT_SCROLL_DEBOUNCE : EMULATE_SCROLL_END_DEBOUNCE;

    fromEvent(element, scrollEndEvent)
      .pipe(
        debounceTime(scrollEndDebounce),
        takeUntilDestroyed(this.destroyRef),
        tap((event) => this.handleScrollEndEvent(event))
      )
      .subscribe();
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.isInitialLoad = false;
    }, INITIAL_LOAD_GRACE_PERIOD);

    this.initScrollHandler();

    this.recalculateImageWidth();

    this.darkness$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'),
      takeUntilDestroyed(this.destroyRef)
    );

    this.readerSettings = toSignal(this.readerSettings$, {injector: this.injector, requireSync: true});

    // Automatically updates when the breakpoint changes, or when reader settings changes
    this.widthOverride = computed(() => {
      const breakpoint = this.breakpointService.activeBreakpoint();
      const value = this.readerSettings().widthSlider;

      if (breakpoint <= this.readingProfile.disableWidthOverride) {
        return '';
      }
      return (parseInt(value) <= 0) ? '' : value + '%';
    });

    // perform jump so the page stays in view
    effect(() => {
      const width = this.widthOverride();
      this.currentPageElem = this.document.querySelector('img#page-' + this.pageNum);
      if(!this.currentPageElem)
        return;

      let images = Array.from(document.querySelectorAll('img[id^="page-"]')) as HTMLImageElement[];
      images.forEach((img) => {
        this.renderer.setStyle(img, "width", width);
      });

      this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
      this.currentPageElem.scrollIntoView();
      this.cdRef.markForCheck();
    }, {injector: this.injector});

    if (this.goToPage) {
      this.goToPage.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(page => {
        const isSamePage = this.pageNum === page;
        if (isSamePage) { return; }
        this.debugLog('[GoToPage] jump has occurred from ' + this.pageNum + ' to ' + page);

        if (this.pageNum < page) {
          this.scrollingDirection = PAGING_DIRECTION.FORWARD;
        } else {
          this.scrollingDirection = PAGING_DIRECTION.BACKWARDS;
        }

        this.setPageNum(page, true);
      });
    }

    if (this.bookmarkPage) {
      this.bookmarkPage.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(page => {
        const image = document.querySelector('img[id^="page-' + page + '"]');
        if (image) {
          this.renderer.addClass(image, 'bookmark-effect');

          setTimeout(() => {
            this.renderer.removeClass(image, 'bookmark-effect');
          }, 1000);
        }
      });
    }

    if (this.fullscreenToggled) {
      this.fullscreenToggled.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(isFullscreen => {
        this.debugLog('[FullScreen] Fullscreen mode: ', isFullscreen);
        this.isFullscreenMode = isFullscreen;
        this.cdRef.markForCheck();

        this.recalculateImageWidth();
        this.initScrollHandler();
        this.setPageNum(this.pageNum, true);
      });
    }
  }


  recalculateImageWidth() {
    const [_, innerWidth] = this.getInnerDimensions();
    this.webtoonImageWidth = innerWidth || document.body.clientWidth || document.documentElement.clientWidth;
    this.cdRef.markForCheck();
  }

  getVerticalOffset() {
    const reader = this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body;

    let offset = 0;
    if (reader instanceof Window) {
      offset = reader.scrollY;
    } else {
      offset = reader.scrollTop;
    }

    return (offset
      || document.body.scrollTop
      || document.documentElement.scrollTop
      || 0);
  }

  /**
   * On scroll in document, calculate if the user/javascript has scrolled to the current image element (and it's visible), update that scrolling has ended completely,
   * and calculate the direction the scrolling is occurring. This is not used for prefetching.
   * @param event Scroll Event
   */
  handleScrollEvent(event?: any) {
    const verticalOffset = this.getVerticalOffset();

    if (verticalOffset > this.prevScrollPosition) {
      this.scrollingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.scrollingDirection = PAGING_DIRECTION.BACKWARDS;
    }
    this.prevScrollPosition = verticalOffset;

    if (this.isScrolling && this.currentPageElem != null && this.isElementVisible(this.currentPageElem)) {
      this.debugLog('[Scroll] Image is visible from scroll, isScrolling is now false');
      this.isScrolling = false;
      this.cdRef.markForCheck();
    }
  }

  handleScrollEndEvent(event?: any) {
    if (this.ignoreNextScrollEvent()) {
      this.ignoreNextScrollEvent.set(false);
      return;
    }

    if (!this.isScrolling) {

      const closestImages = Array.from(document.querySelectorAll('img[id^="page-"]')) as HTMLImageElement[];
      const img = this.findClosestVisibleImage(closestImages);

      if (img != null) {
        this.setPageNum(parseInt(img.getAttribute('page') || this.pageNum + '', 10));
      }
    }
  }

  getTotalHeight() {
    let totalHeight = 0;
    document.querySelectorAll('img[id^="page-"]').forEach(img => totalHeight += img.getBoundingClientRect().height);
    return Math.round(totalHeight);
  }

  getTotalScroll() {
    if (this.isFullscreenMode) {
      return this.readerElemRef.nativeElement.offsetHeight + this.readerElemRef.nativeElement.scrollTop;
    }
    return document.body.offsetHeight + document.body.scrollTop;
  }

  getScrollTop() {
    if (this.isFullscreenMode) {
      return this.readerElemRef.nativeElement.scrollTop;
    }
    return document.body.scrollTop;
  }

  /**
   *
   * @returns Height, Width
   */
  getInnerDimensions() {
    let innerHeight = window.innerHeight;
    let innerWidth = window.innerWidth;

    if (this.isFullscreenMode) {
      innerHeight = this.readerElemRef.nativeElement.clientHeight;
      innerWidth = this.readerElemRef.nativeElement.clientWidth;
    }
    return [innerHeight, innerWidth];
  }

  /**
   * Is any part of the element visible in the scrollport. Does not take into account
   * style properties, just scroll port visibility.
   * @param elem
   * @returns
   */
  isElementVisible(elem: Element) {
    if (elem === null || elem === undefined) { return false; }

    this.debugLog('[Visibility] Checking if Page ' + elem.getAttribute('id') + ' is visible');
    // NOTE: This will say an element is visible if it is 1 px offscreen on top
    const rect = elem.getBoundingClientRect();

    const [innerHeight, innerWidth] = this.getInnerDimensions();

    return (rect.bottom >= 0 &&
            rect.right >= 0 &&
            rect.top <= (innerHeight || document.body.clientHeight) &&
            rect.left <= (innerWidth || document.body.clientWidth)
          );
  }

  /**
   * Is any part of the element visible in the scrollport and is it above the midline trigger.
   * The midline trigger does not mean it is half of the screen. It may be top 25%.
   * @param elem HTML Element
   * @returns If above midline
   */
   shouldElementCountAsCurrentPage(elem: Element) {
    if (elem === null || elem === undefined) { return false; }

    const rect = elem.getBoundingClientRect();
    const [innerHeight, innerWidth] = this.getInnerDimensions();

    if (rect.bottom >= 0 &&
            rect.right >= 0 &&
            rect.top <= (innerHeight || document.body.clientHeight) &&
            rect.left <= (innerWidth || document.body.clientWidth)
          ) {
            const topX = (innerHeight || document.body.clientHeight);
            return Math.abs(rect.top / topX) <= 0.25;
          }
    return false;
  }

  /**
   * Find the closest visible image within the viewport.
   * @param images An array of HTML Image Elements
   * @returns Closest visible image or null if none are visible
   */
  findClosestVisibleImage(images: HTMLImageElement[]): HTMLImageElement | null {
    let closestImage: HTMLImageElement | null = null;
    let closestDistanceToTop = Number.MAX_VALUE; // Initialize to a high value.

    for (const image of images) {
      // Get the bounding rectangle of the image.
      const rect = image.getBoundingClientRect();

      // Calculate the distance of the current image to the top of the viewport.
      const distanceToTop = Math.abs(rect.top);

      // Check if the image is visible within the viewport.
      if (distanceToTop < closestDistanceToTop) {
        closestDistanceToTop = distanceToTop;
        closestImage = image;
      }
    }

    return closestImage;
  }


  initWebtoonReader() {
    this.initFinished = false;
    this.recalculateImageWidth();
    this.imagesLoaded = {};
    this.webtoonImages.next([]);
    this.retryImages = new Queue<{page: number, src: string, chapterId: number, retryCount: number}>();
    this.atBottom = false;
    this.cdRef.markForCheck();
    const [startingIndex, endingIndex] = this.calculatePrefetchIndecies();


    this.debugLog('[INIT] Prefetching pages ' + startingIndex + ' to ' + endingIndex + '. Current page: ', this.pageNum);
    for(let i = startingIndex; i <= endingIndex; i++) {
      this.loadWebtoonImage(i);
    }
    this.cdRef.markForCheck();
  }

  /**
   * Callback for an image onLoad. At this point the image is already rendered in DOM (may not be visible)
   * This will be used to scroll to current page for intial load
   * @param event
   */
  onImageLoad(event: any) {
    const imagePage = this.readerService.imageUrlToPageNum(event.target.src);
    this.debugLog('[Image Load] Image loaded: ', imagePage);

    if (event.target.width < this.webtoonImageWidth) {
      this.webtoonImageWidth = event.target.width;
    }

    this.renderer.setAttribute(event.target, 'width', this.mangaReaderService.maxWidth() + '');
    this.renderer.setAttribute(event.target, 'height', event.target.height + '');

    this.attachIntersectionObserverElem(event.target);

    if (imagePage === this.pageNum) {
      Promise.all(Array.from(this.document.querySelectorAll('img'))
        .filter((img: any) => !img.complete)
        .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
        .then(() => {
          this.debugLog('[Initialization] All images have loaded from initial prefetch, initFinished = true');
          this.debugLog('[Image Load] ! Loaded current page !', this.pageNum);
          this.currentPageElem = this.document.querySelector('img#page-' + this.pageNum);
          // There needs to be a bit of time before we scroll
          if (this.currentPageElem && !this.isElementVisible(this.currentPageElem)) {
            this.scrollToCurrentPage();
          } else {
            this.initFinished = true;
            this.cdRef.markForCheck();
          }

          this.allImagesLoaded = true;
          this.cdRef.markForCheck();
      });
    }
  }

  onImageLoadError(event: any) {
    const imagePage = this.readerService.imageUrlToPageNum(event.target.src);
    const chapterId = this.readerService.imageUrlToChapterId(event.target.src);
    this.debugLog('[Image Error] Failed to load page: ', imagePage);

    // Let's set the height of the img since we already know it then retry
    const dimensions = this.mangaReaderService.getPageDimensions(imagePage);
    if (dimensions?.height) {
      this.renderer.setStyle(event.target, 'height', dimensions?.height + 'px');
      this.renderer.setStyle(event.target, 'border', '1px solid red');
    }

    this.retryImages.enqueue({retryCount: 0, page: imagePage, src: event.target.src, chapterId: chapterId});
    this.processImageRetry();
  }

  private async processImageRetry() {
    if (this.isProcessingRetries) return;
    this.isProcessingRetries = true;

    try {
      while (!this.retryImages.isEmpty()) {
        const item = this.retryImages.dequeue();
        if (!item) continue;

        this.debugLog('Retrying failed load of page ' +  item.page, ' retry count: ' + item.retryCount)
        // Skip stale (chapter id has changed)
        if (item?.chapterId !== this.chapterId) continue;

        // Skip descoped DOM
        const pageElem = this.document.querySelector('img#page-' + item.page) as HTMLImageElement;
        if (!pageElem) continue;

        const urlWithoutRetry = item.src.split('&retry=')[0];
        pageElem.src = urlWithoutRetry + '&retry=' + item.retryCount;

        const success = await this.waitForLoadOrError(pageElem);

        if (success) {
          this.debugLog('Resolved a failed load for page: ', item.page);
          // Remove the error styling
          this.renderer.removeStyle(pageElem, 'border');
          this.renderer.removeStyle(pageElem, 'height');
          this.onImageLoad({ target: pageElem });
        } else if (item.retryCount < MAX_FAILED_IMG_RETRIES) {
          item.retryCount++;
          this.retryImages.enqueue(item);
          await this.delay(1000 * item.retryCount); // Backoff pressure
        } else {
          console.error('Failed to load page ' + item.page + ' for chapter ' + item.chapterId + ' after ' + MAX_FAILED_IMG_RETRIES + ' retries');
        }
      }
    } finally {
      this.isProcessingRetries = false;
    }
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  private waitForLoadOrError(img: HTMLImageElement): Promise<boolean> {
    return new Promise(resolve => {
      const cleanup = () => {
        img.onload = null;
        img.onerror = null;
        clearTimeout(timer);
      };
      // Allow the image to load or timeout after ~10 seconds
      const timer = setTimeout(() => { cleanup(); resolve(false); }, IMAGE_RETRY_TIMEOUT_MS);
      img.onload = () => { cleanup(); resolve(true); };
      img.onerror = () => { cleanup(); resolve(false); };
    });
  }


  handleIntersection(entries: IntersectionObserverEntry[]) {
    if (!this.allImagesLoaded || this.isScrolling) {
      this.debugLog('[Intersection] Images are not loaded (or performing scrolling action), skipping any scroll calculations');
      return;
    }

    entries.forEach(entry => {
      const imagePage = parseInt(entry.target.attributes.getNamedItem('page')?.value + '', 10);
      this.debugLog('[Intersection] Page ' + imagePage + ' is visible: ', entry.isIntersecting);
      if (entry.isIntersecting) {
        this.debugLog('[Intersection] ! Page ' + imagePage + ' just entered screen');
        this.prefetchWebtoonImages(imagePage);
      }
    });
  }

  /**
   * Move to the next chapter and set the page
   */
  moveToNextChapter() {
    if (!this.allImagesLoaded) return;

    this.setPageNum(this.totalPages);
    this.loadNextChapter.emit(undefined);
  }

  /**
   * Set the page number, invoke prefetching and optionally scroll to the new page.
   * @param pageNum Page number to set to. Will trigger the pageNumberChange event emitter.
   * @param scrollToPage Optional (default false) parameter to trigger scrolling to the newly set page
   */
  setPageNum(pageNum: number, scrollToPage: boolean = false) {
    if (pageNum >= this.totalPages) {
      pageNum = this.totalPages - 1;
    } else if (pageNum < 0) {
      pageNum = 0;
    }

    this.pageNum = pageNum;
    this.pageChangeSubject.next(this.pageNum);

    this.cdRef.markForCheck();

    this.prefetchWebtoonImages();

    if (scrollToPage) {
      this.scrollToCurrentPage();
    }
  }

  isScrollingForwards() {
    return this.scrollingDirection === PAGING_DIRECTION.FORWARD;
  }

  /**
   * Performs the scroll for the current page element. Updates any state variables needed.
   */
  scrollToCurrentPage() {
    this.currentPageElem = document.querySelector('img#page-' + this.pageNum);
    if (!this.currentPageElem) { return; }
    this.debugLog('[GoToPage] Scrolling to page', this.pageNum);

    // Update prevScrollPosition, so the next scroll event properly calculates direction
    this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
    this.isScrolling = true;
    this.cdRef.markForCheck();

    setTimeout(() => {
      if (this.currentPageElem) {
        this.debugLog('[Scroll] Scrolling to page ', this.pageNum);
        this.currentPageElem.scrollIntoView({behavior: 'smooth'});
        this.initFinished = true;
        this.cdRef.markForCheck();
      }
    }, 600);
  }

  loadWebtoonImage(page: number) {
    if (this.imagesLoaded.hasOwnProperty(page)) {
      this.debugLog('\t[PREFETCH] Skipping prefetch of ', page);
      return;
    }

    this.debugLog('\t[PREFETCH] Prefetching ', page);

    const data = this.webtoonImages.value.concat({src: this.urlProvider(page), page});

    data.sort((a: WebtoonImage, b: WebtoonImage) => {
      if (a.page < b.page) { return -1; }
      else if (a.page > b.page) { return 1; }
      else return 0;
    });

    this.allImagesLoaded = false;
    this.cdRef.markForCheck();
    this.webtoonImages.next(data);

    if (!this.imagesLoaded.hasOwnProperty(page)) {
      this.imagesLoaded[page] = page;
    }
  }

  attachIntersectionObserverElem(elem: HTMLImageElement) {
    if (elem !== null) {
      this.intersectionObserver.observe(elem);
      this.debugLog('[Intersection] Attached Intersection Observer to page', this.readerService.imageUrlToPageNum(elem.src));
    } else {
      console.error('Could not attach observer on elem'); // This never happens
    }
  }

  /**
   * Finds the ranges of indecies to load from backend. totalPages - 1 is due to backend will automatically return last page for any page number
   * above totalPages. Webtoon reader might ask for that which results in duplicate last pages.
   * @param pageNum
   * @returns
   */
  calculatePrefetchIndecies(pageNum: number = -1) {
    if (pageNum == -1) {
      pageNum = this.pageNum;
    }

    let startingIndex = 0;
    let endingIndex = 0;
    if (this.isScrollingForwards()) {
      startingIndex = Math.min(Math.max(pageNum - this.bufferPages, 0), this.totalPages - 1);
      endingIndex = Math.min(Math.max(pageNum + this.bufferPages, 0), this.totalPages - 1);

      if (startingIndex === this.totalPages) {
        return [0, 0];
      }
    } else {
      startingIndex = Math.min(Math.max(pageNum - this.bufferPages, 0), this.totalPages - 1);
      endingIndex = Math.min(Math.max(pageNum + this.bufferPages, 0), this.totalPages - 1);
    }


    if (startingIndex > endingIndex) {
      const temp = startingIndex;
      startingIndex = endingIndex;
      endingIndex = temp;
    }

    return [startingIndex, endingIndex];
  }

  range(size: number, startAt: number = 0): ReadonlyArray<number> {
    return [...Array(size).keys()].map(i => i + startAt);
  }

  prefetchWebtoonImages(pageNum: number = -1) {
    if (pageNum === -1) {
      pageNum = this.pageNum;
    }

    const [startingIndex, endingIndex] = this.calculatePrefetchIndecies(pageNum);
    if (startingIndex === 0 && endingIndex === 0) { return; }

    this.debugLog('\t[PREFETCH] prefetching pages: ' + startingIndex + ' to ' + endingIndex);
    for(let i = startingIndex; i <= endingIndex; i++) {
      this.loadWebtoonImage(i);
    }

    Promise.all(Array.from(document.querySelectorAll('img'))
      .filter((img: any) => !img.complete)
      .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
      .then(() => {
        this.allImagesLoaded = true;
        this.cdRef.markForCheck();
    });
  }

  debugLog(message: string, extraData?: any) {
    if (!(this.debugMode & DEBUG_MODES.Logs)) return;

    if (this.debugLogFilter.filter(str => message.replace('\t', '').startsWith(str)).length > 0) return;
    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }

  showDebugBar() {
    return this.debugMode & DEBUG_MODES.ActionBar;
  }

  showDebugOutline() {
    return this.debugMode & DEBUG_MODES.Outline;
  }
}
