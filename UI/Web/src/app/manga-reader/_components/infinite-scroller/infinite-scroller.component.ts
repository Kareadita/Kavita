import {DOCUMENT} from '@angular/common';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  model,
  OnInit,
  output,
  Renderer2,
  signal,
  Signal,
  untracked,
  viewChild
} from '@angular/core';
import {BehaviorSubject, fromEvent, Observable, ReplaySubject, tap} from 'rxjs';
import {debounceTime} from 'rxjs/operators';
import {ReaderService} from '../../../_services/reader.service';
import {PAGING_DIRECTION} from '../../_models/reader-enums';
import {WebtoonImage} from '../../_models/webtoon-image';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {InfiniteScrollDirective} from "ngx-infinite-scroll";
import {ReaderSetting} from "../../_models/reader-setting";
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
 * How many times the Webtoon reader will retry failed images
 */
const MAX_FAILED_IMG_RETRIES = 3;
/**
 * How long to wait for an image load/error event before treating it as a failure
 */
const IMAGE_RETRY_TIMEOUT_MS = 10_000;
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
  imports: [TranslocoDirective, InfiniteScrollDirective, PullToLoadComponent]
})
export class InfiniteScrollerComponent implements OnInit {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly mangaReaderService = inject(MangaReaderService);
  private readonly readerService = inject(ReaderService);
  private readonly renderer = inject(Renderer2);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly breakpointService = inject(BreakpointService);

  scrollContainer = viewChild.required<ElementRef<HTMLDivElement>>('scroller');
  pullToLoadNext = viewChild<PullToLoadComponent>('pullToLoadNext');
  ignoreNextScrollEvent = signal(false);

  /**
   * Current page number aka what's recorded on screen
   */
  pageNum = model.required<number>();
  /**
   * Number of pages to prefetch ahead of position
   */
  bufferPages = input<number>(5);
  /**
   * Total number of pages
   */
  totalPages = input.required<number>();
  /**
   * Method to generate the src for Image loading
   */
  urlProvider = input.required<(page: number) => string>();
  readerSettings$ = input.required<Observable<ReaderSetting>>();
  readingProfile = input.required<ReadingProfile>();
  chapterId = input.required<number>();

  readonly loadNextChapter = output<void>();
  readonly loadPrevChapter = output<void>();

  goToPage = input<BehaviorSubject<number>>();
  bookmarkPage = input<ReplaySubject<number>>();
  fullscreenToggled = input<ReplaySubject<boolean>>();

  readerElemRef!: ElementRef<HTMLDivElement>;

  /**
   * Stores and emits all the src urls
   */
  webtoonImages = signal<WebtoonImage[]>([]);
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
  scrollingDirection = signal<PAGING_DIRECTION>(PAGING_DIRECTION.FORWARD);
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
  webtoonImageWidth = signal<number>(window.innerWidth || this.document.body.clientWidth || this.document.documentElement.clientWidth);
  /**
   * Used to tell if a scrollTo() operation is in progress
   */
  isScrolling = signal(false);
  /**
   * Whether all prefetched images have loaded on the screen (not necessarily in viewport)
   */
  allImagesLoaded = signal<boolean>(false);
  /**
   * Pages that have been queued for loading. If pruning is implemented, the page will be removed.
   */
  imagesLoaded = signal<Set<number>>(new Set());
  /**
   * If the manga reader is in fullscreen. Some math changes based on this value.
   */
  isFullscreenMode = signal(false);
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

  darknessStyle = computed(() => {
    return 'brightness(' + this.readerSettings().darkness + '%)';
  });
  isScrollingForwards = computed(() => this.scrollingDirection() === PAGING_DIRECTION.FORWARD);
  minPageLoaded = computed(() => Math.min(...this.imagesLoaded()));
  maxPageLoaded = computed(() => Math.max(...this.imagesLoaded()));
  scrollElement = computed<HTMLElement>(() => this.isFullscreenMode() ? this.readerElemRef.nativeElement : this.document.body);

  /**
   * Kept as a getter (not computed) since it also reads window dimensions, which aren't reactive
   */
  get areImagesWiderThanWindow() {
    let [_, innerWidth] = this.getInnerDimensions();
    return this.webtoonImageWidth() > (innerWidth || document.body.clientWidth);
  }

  constructor() {
    const document = this.document;

    // This will always exist at this point in time since this is used within manga reader
    const reader = document.querySelector('.reading-area');
    if (reader !== null) {
      this.readerElemRef = new ElementRef(reader as HTMLDivElement);
    }

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


    // Trigger initWebtoonReader when totalPages is set/changed
    effect(() => {
      this.totalPages();
      untracked(() => this.initWebtoonReader());
    });

    afterNextRender(() => this.scrollContainer().nativeElement.focus());

    this.destroyRef.onDestroy(() => this.intersectionObserver.disconnect());
  }

  /**
   * Responsible for binding the scroll handler to the correct event. On non-fullscreen, body is correct. However, on fullscreen, we must use the reader as that is what
   * gets promoted to fullscreen.
   */
  initScrollHandler() {
    const element = this.scrollElement();

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
    this.initScrollHandler();

    this.recalculateImageWidth();


    // TODO: Can this be a linkedSignal?
    this.readerSettings = toSignal(this.readerSettings$(), {injector: this.injector, requireSync: true});

    // Automatically updates when the breakpoint changes, or when reader settings changes
    this.widthOverride = computed(() => {
      const breakpoint = this.breakpointService.activeBreakpoint();
      const value = this.readerSettings().widthSlider;

      if (breakpoint <= this.readingProfile().disableWidthOverride) {
        return '';
      }
      return (value <= 0) ? '' : value + '%';
    });

    // perform jump so the page stays in view. Only width changes should trigger this, not page changes
    effect(() => {
      const width = this.widthOverride();
      const pageNum = untracked(this.pageNum);
      this.currentPageElem = this.document.querySelector('img#page-' + pageNum);
      if(!this.currentPageElem)
        return;

      const images = Array.from(document.querySelectorAll('img[id^="page-"]')) as HTMLImageElement[];
      images.forEach((img) => {
        this.renderer.setStyle(img, "width", width);

        if (img.naturalWidth > 0 && img.naturalHeight > 0) {
          const height = Math.round(img.getBoundingClientRect().width * img.naturalHeight / img.naturalWidth);
          this.renderer.setAttribute(img, 'height', height + '');
        }
      });

      this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
      this.currentPageElem.scrollIntoView();
    }, {injector: this.injector});

    const goToPage = this.goToPage();
    if (goToPage) {
      goToPage.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(page => {
        const isSamePage = this.pageNum() === page;
        if (isSamePage) { return; }
        this.debugLog('[GoToPage] jump has occurred from ' + this.pageNum() + ' to ' + page);

        if (this.pageNum() < page) {
          this.scrollingDirection.set(PAGING_DIRECTION.FORWARD);
        } else {
          this.scrollingDirection.set(PAGING_DIRECTION.BACKWARDS);
        }

        this.setPageNum(page, true);
      });
    }

    const bookmarkPage = this.bookmarkPage();
    if (bookmarkPage) {
      bookmarkPage.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(page => {
        const image = document.querySelector('img[id^="page-' + page + '"]');
        if (image) {
          this.renderer.addClass(image, 'bookmark-effect');

          setTimeout(() => {
            this.renderer.removeClass(image, 'bookmark-effect');
          }, 1000);
        }
      });
    }

    const fullscreenToggled = this.fullscreenToggled();
    if (fullscreenToggled) {
      fullscreenToggled.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(isFullscreen => {
        this.debugLog('[FullScreen] Fullscreen mode: ', isFullscreen);
        this.isFullscreenMode.set(isFullscreen);

        this.recalculateImageWidth();
        this.initScrollHandler();
        this.setPageNum(this.pageNum(), true);
      });
    }
  }


  recalculateImageWidth() {
    const [_, innerWidth] = this.getInnerDimensions();
    this.webtoonImageWidth.set(innerWidth || document.body.clientWidth || document.documentElement.clientWidth);
  }

  getVerticalOffset() {
    const reader = this.scrollElement();

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
      this.scrollingDirection.set(PAGING_DIRECTION.FORWARD);
    } else {
      this.scrollingDirection.set(PAGING_DIRECTION.BACKWARDS);
    }
    this.prevScrollPosition = verticalOffset;

    if (this.isScrolling() && this.currentPageElem != null && this.isElementVisible(this.currentPageElem)) {
      this.debugLog('[Scroll] Image is visible from scroll, isScrolling is now false');
      this.isScrolling.set(false);
    }
  }

  handleScrollEndEvent(event?: any) {
    if (this.ignoreNextScrollEvent()) {
      this.ignoreNextScrollEvent.set(false);
      return;
    }

    if (!this.isScrolling()) {

      const closestImages = Array.from(document.querySelectorAll('img[id^="page-"]')) as HTMLImageElement[];
      const img = this.findClosestVisibleImage(closestImages);

      if (img != null) {
        this.setPageNum(parseInt(img.getAttribute('page') || this.pageNum() + '', 10));
      }
    }
  }

  getTotalHeight() {
    let totalHeight = 0;
    document.querySelectorAll('img[id^="page-"]').forEach(img => totalHeight += img.getBoundingClientRect().height);
    return Math.round(totalHeight);
  }

  getTotalScroll() {
    if (this.isFullscreenMode()) {
      return this.readerElemRef.nativeElement.offsetHeight + this.readerElemRef.nativeElement.scrollTop;
    }
    return document.body.offsetHeight + document.body.scrollTop;
  }

  getScrollTop() {
    if (this.isFullscreenMode()) {
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

    if (this.isFullscreenMode()) {
      innerHeight = this.readerElemRef.nativeElement.clientHeight;
      innerWidth = this.readerElemRef.nativeElement.clientWidth;
    }
    return [innerHeight, innerWidth];
  }

  /**
   * Is any part of the element visible in the scroll port. Does not take into account
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
    this.recalculateImageWidth();
    this.imagesLoaded.set(new Set());
    this.webtoonImages.set([]);
    this.retryImages = new Queue<{page: number, src: string, chapterId: number, retryCount: number}>();
    const [startingIndex, endingIndex] = this.calculatePrefetchIndices();


    this.debugLog('[INIT] Prefetching pages ' + startingIndex + ' to ' + endingIndex + '. Current page: ', this.pageNum());
    for(let i = startingIndex; i <= endingIndex; i++) {
      this.loadWebtoonImage(i);
    }
  }

  /**
   * Callback for an image onLoad. At this point the image is already rendered in DOM (may not be visible)
   * This will be used to scroll to current page for intial load
   * @param img The image that loaded
   */
  onImageLoad(img: HTMLImageElement) {
    const imagePage = this.readerService.imageUrlToPageNum(img.src);
    this.debugLog('[Image Load] Image loaded: ', imagePage);

    if (img.width < this.webtoonImageWidth()) {
      this.webtoonImageWidth.set(img.width);
    }

    this.renderer.setAttribute(img, 'width', this.mangaReaderService.maxWidth() + '');
    this.renderer.setAttribute(img, 'height', img.height + '');

    this.attachIntersectionObserverElem(img);

    if (imagePage === this.pageNum()) {
      Promise.all(Array.from(this.document.querySelectorAll('img'))
        .filter((pending: any) => !pending.complete)
        .map((pending: any) => new Promise(resolve => { pending.onload = pending.onerror = resolve; })))
        .then(() => {
          this.debugLog('[Initialization] All images have loaded from initial prefetch');
          this.debugLog('[Image Load] ! Loaded current page !', this.pageNum());
          this.currentPageElem = this.document.querySelector('img#page-' + this.pageNum());
          // There needs to be a bit of time before we scroll
          if (this.currentPageElem && !this.isElementVisible(this.currentPageElem)) {
            this.scrollToCurrentPage();
          }

          this.allImagesLoaded.set(true);
      });
    }
  }

  onImageLoadError(img: HTMLImageElement) {
    const imagePage = this.readerService.imageUrlToPageNum(img.src);
    const chapterId = this.readerService.imageUrlToChapterId(img.src);
    this.debugLog('[Image Error] Failed to load page: ', imagePage);

    // Let's set the height of the img since we already know it then retry
    const dimensions = this.mangaReaderService.getPageDimensions(imagePage);
    if (dimensions?.height) {
      this.renderer.setStyle(img, 'height', dimensions?.height + 'px');
      this.renderer.setStyle(img, 'border', '1px solid red');
    }

    this.retryImages.enqueue({retryCount: 0, page: imagePage, src: img.src, chapterId: chapterId});
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
        if (item?.chapterId !== this.chapterId()) continue;

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
          this.onImageLoad(pageElem);
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
    if (!this.allImagesLoaded() || this.isScrolling()) {
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
    if (!this.allImagesLoaded()) return;

    this.setPageNum(this.totalPages());
    this.loadNextChapter.emit(undefined);
  }

  /**
   * Set the page number, invoke prefetching and optionally scroll to the new page.
   * @param pageNum Page number to set to. Emits pageNumChange when the page actually changes.
   * @param scrollToPage Optional (default false) parameter to trigger scrolling to the newly set page
   */
  setPageNum(pageNum: number, scrollToPage: boolean = false) {
    if (pageNum >= this.totalPages()) {
      pageNum = this.totalPages() - 1;
    } else if (pageNum < 0) {
      pageNum = 0;
    }

    this.pageNum.set(pageNum);

    this.prefetchWebtoonImages();

    if (scrollToPage) {
      this.scrollToCurrentPage();
    }
  }



  /**
   * Performs the scroll for the current page element. Updates any state variables needed.
   */
  scrollToCurrentPage() {
    this.currentPageElem = document.querySelector('img#page-' + this.pageNum());
    if (!this.currentPageElem) { return; }
    this.debugLog('[GoToPage] Scrolling to page', this.pageNum());

    // Update prevScrollPosition, so the next scroll event properly calculates direction
    this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
    this.isScrolling.set(true);

    setTimeout(() => {
      if (this.currentPageElem) {
        this.debugLog('[Scroll] Scrolling to page ', this.pageNum());
        this.currentPageElem.scrollIntoView({behavior: 'smooth'});
      }
    }, 600);
  }

  loadWebtoonImage(page: number) {
    if (this.imagesLoaded().has(page)) {
      this.debugLog('\t[PREFETCH] Skipping prefetch of ', page);
      return;
    }

    this.debugLog('\t[PREFETCH] Prefetching ', page);

    this.webtoonImages.update(images =>
      [...images, {src: this.urlProvider()(page), page}].sort((a, b) => a.page - b.page)
    );
    this.allImagesLoaded.set(false);
    this.imagesLoaded.update(loaded => new Set(loaded).add(page));
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
   * Finds the ranges of indices to load from backend. totalPages - 1 is due to backend will automatically return last page for any page number
   * above totalPages. Webtoon reader might ask for that which results in duplicate last pages.
   * @param pageNum
   * @returns
   */
  calculatePrefetchIndices(pageNum: number = -1) {
    if (pageNum == -1) {
      pageNum = this.pageNum();
    }

    let startingIndex = Math.min(Math.max(pageNum - this.bufferPages(), 0), this.totalPages() - 1);
    let endingIndex = Math.min(Math.max(pageNum + this.bufferPages(), 0), this.totalPages() - 1);

    if (startingIndex > endingIndex) {
      const temp = startingIndex;
      startingIndex = endingIndex;
      endingIndex = temp;
    }

    return [startingIndex, endingIndex];
  }

  prefetchWebtoonImages(pageNum: number = -1) {
    if (pageNum === -1) {
      pageNum = this.pageNum();
    }

    const [startingIndex, endingIndex] = this.calculatePrefetchIndices(pageNum);
    if (startingIndex === 0 && endingIndex === 0) { return; }

    this.debugLog('\t[PREFETCH] prefetching pages: ' + startingIndex + ' to ' + endingIndex);
    for(let i = startingIndex; i <= endingIndex; i++) {
      this.loadWebtoonImage(i);
    }

    Promise.all(Array.from(document.querySelectorAll('img'))
      .filter((img: any) => !img.complete)
      .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
      .then(() => {
        this.allImagesLoaded.set(true);
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
