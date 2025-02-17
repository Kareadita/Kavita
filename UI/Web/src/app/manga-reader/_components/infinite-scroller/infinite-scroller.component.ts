import {DOCUMENT, AsyncPipe, NgStyle} from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  EventEmitter,
  inject,
  Inject,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  Renderer2,
  SimpleChanges, ViewChild
} from '@angular/core';
import {BehaviorSubject, fromEvent, map, Observable, of, ReplaySubject} from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { ScrollService } from 'src/app/_services/scroll.service';
import { ReaderService } from '../../../_services/reader.service';
import { PAGING_DIRECTION } from '../../_models/reader-enums';
import { WebtoonImage } from '../../_models/webtoon-image';
import { MangaReaderService } from '../../_service/manga-reader.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {InfiniteScrollModule} from "ngx-infinite-scroll";
import {ReaderSetting} from "../../_models/reader-setting";
import {SafeStylePipe} from "../../../_pipes/safe-style.pipe";

/**
 * How much additional space should pass, past the original bottom of the document height before we trigger the next chapter load
 */
const SPACER_SCROLL_INTO_PX = 200;

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
    standalone: true,
  imports: [AsyncPipe, TranslocoDirective, InfiniteScrollModule, SafeStylePipe, NgStyle]
})
export class InfiniteScrollerComponent implements OnInit, OnChanges, OnDestroy, AfterViewInit {

  private readonly mangaReaderService = inject(MangaReaderService);
  private readonly readerService = inject(ReaderService);
  private readonly renderer = inject(Renderer2);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

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
  @Output() pageNumberChange: EventEmitter<number> = new EventEmitter<number>();
  @Output() loadNextChapter: EventEmitter<void> = new EventEmitter<void>();
  @Output() loadPrevChapter: EventEmitter<void> = new EventEmitter<void>();

  @Input() goToPage: BehaviorSubject<number> | undefined;
  @Input() bookmarkPage: ReplaySubject<number> = new ReplaySubject<number>();
  @Input() fullscreenToggled: ReplaySubject<boolean> = new ReplaySubject<boolean>();

  @ViewChild('bottomSpacer', {static: false}) bottomSpacer!: ElementRef;
  bottomSpacerIntersectionObserver: IntersectionObserver = new IntersectionObserver((entries) => this.handleBottomIntersection(entries),
    { threshold: 1.0 });

  darkness$: Observable<string> = of('brightness(100%)');

  readerElemRef!: ElementRef<HTMLDivElement>;

  /**
   * Stores and emits all the src urls
   */
  webtoonImages: BehaviorSubject<WebtoonImage[]> = new BehaviorSubject<WebtoonImage[]>([]);

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
    * Keeps track of the previous scrolling height for restoring scroll position after we inject spacer block
    */
   previousScrollHeightMinusTop: number = 0;
   /**
    * Tracks the first load, until all the initial prefetched images are loaded. We use this to reduce opacity so images can load without jerk.
    */
   initFinished: boolean = false;
  /**
   * Debug mode. Will show extra information. Use bitwise (|) operators between different modes to enable different output
   */
  debugMode: DEBUG_MODES = DEBUG_MODES.None;
  /**
   * Debug mode. Will filter out any messages in here so they don't hit the log
   */
  debugLogFilter: Array<string> = ['[PREFETCH]', '[Intersection]', '[Visibility]', '[Image Load]'];

  /**
   * Width override for manual width control
   * 2 observables needed to avoid flickering, probably due to data races, when changing the width
   * this allows to precisely define execution order
  */
  widthOverride$ : Observable<string> = new Observable<string>();
  widthSliderValue$ : Observable<string> = new Observable<string>();

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

  constructor(@Inject(DOCUMENT) private readonly document: Document) {
    // This will always exist at this point in time since this is used within manga reader
    const reader = document.querySelector('.reading-area');
    if (reader !== null) {
      this.readerElemRef = new ElementRef(reader as HTMLDivElement);
    }
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

  /**
   * Responsible for binding the scroll handler to the correct event. On non-fullscreen, body is correct. However, on fullscreen, we must use the reader as that is what
   * gets promoted to fullscreen.
   */
  initScrollHandler() {
    //console.log('Setting up Scroll handler on ', this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body);
    fromEvent(this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body, 'scroll')
    .pipe(debounceTime(20), takeUntilDestroyed(this.destroyRef))
    .subscribe((event) => this.handleScrollEvent(event));

    fromEvent(this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body, 'scrollend')
    .pipe(debounceTime(20), takeUntilDestroyed(this.destroyRef))
    .subscribe((event) => this.handleScrollEndEvent(event));
  }

  ngOnInit(): void {
    this.initScrollHandler();

    this.recalculateImageWidth();

    this.darkness$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'),
      takeUntilDestroyed(this.destroyRef)
    );


    this.widthSliderValue$ = this.readerSettings$.pipe(
      map(values => (parseInt(values.widthSlider) <= 0) ? '' : values.widthSlider + '%'),
      takeUntilDestroyed(this.destroyRef)
    );

    this.widthOverride$ = this.widthSliderValue$;

    //perform jump so the page stays in view
    this.widthSliderValue$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(val => {
      this.currentPageElem = this.document.querySelector('img#page-' + this.pageNum);
      if(!this.currentPageElem)
        return;

      let images = Array.from(document.querySelectorAll('img[id^="page-"]')) as HTMLImageElement[];
      images.forEach((img) => {
        this.renderer.setStyle(img, "width", val);
      });

      this.widthOverride$ = this.widthSliderValue$;
      this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
      this.currentPageElem.scrollIntoView();
      this.cdRef.markForCheck();
    });

    if (this.goToPage) {
      this.goToPage.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(page => {
        const isSamePage = this.pageNum === page;
        if (isSamePage) { return; }
        this.debugLog('[GoToPage] jump has occured from ' + this.pageNum + ' to ' + page);

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

  ngAfterViewInit() {
    this.bottomSpacerIntersectionObserver.observe(this.bottomSpacer.nativeElement);
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
   * and calculate the direction the scrolling is occuring. This is not used for prefetching.
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

    if (!this.isScrolling) {
      // Use offset of the image against the scroll container to test if the most of the image is visible on the screen. We can use this
      // to mark the current page and separate the prefetching code.
      const midlineImages = Array.from(document.querySelectorAll('img[id^="page-"]'))
      .filter(entry => this.shouldElementCountAsCurrentPage(entry));

      if (midlineImages.length > 0) {
        this.setPageNum(parseInt(midlineImages[0].getAttribute('page') || this.pageNum + '', 10));
      }
    }

    // Check if we hit the last page
    this.checkIfShouldTriggerContinuousReader();
  }

  handleScrollEndEvent(event?: any) {
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

  checkIfShouldTriggerContinuousReader() {
    if (this.isScrolling) return;

    if (this.scrollingDirection === PAGING_DIRECTION.FORWARD) {
      const totalHeight = this.getTotalHeight();
      const totalScroll = this.getTotalScroll();

      // If we were at top but have started scrolling down past page 0, remove top spacer
      if (this.atTop && this.pageNum > 0) {
        this.atTop = false;
        this.cdRef.markForCheck();
      }

      if (totalHeight != 0 && totalScroll >= totalHeight && !this.atBottom) {
        this.atBottom = true;
        this.cdRef.markForCheck();
        this.setPageNum(this.totalPages);

        // Scroll user back to original location
        this.previousScrollHeightMinusTop = this.getScrollTop();
        requestAnimationFrame(() => {
          document.body.scrollTop = this.previousScrollHeightMinusTop + (SPACER_SCROLL_INTO_PX / 2);
          this.cdRef.markForCheck();
        });
        this.checkIfShouldTriggerContinuousReader()
      } else if (totalScroll >= totalHeight + SPACER_SCROLL_INTO_PX && this.atBottom) {
        // This if statement will fire once we scroll into the spacer at all
        this.loadNextChapter.emit();
        this.cdRef.markForCheck();
      }
    } else {
      // < 5 because debug mode and FF (mobile) can report non 0, despite being at 0
      if (this.getScrollTop() < 5 && this.pageNum === 0 && !this.atTop) {
        this.atBottom = false;
        this.atTop = true;
        this.cdRef.markForCheck();

        // Scroll user back to original location
        this.previousScrollHeightMinusTop = document.body.scrollHeight - document.body.scrollTop;

        const reader = this.isFullscreenMode ? this.readerElemRef.nativeElement : this.document.body;
        requestAnimationFrame(() => this.scrollService.scrollTo((SPACER_SCROLL_INTO_PX / 2), reader));
      } else if (this.getScrollTop() < 5 && this.pageNum === 0 && this.atTop) {
        // If already at top, then we moving on
        this.loadPrevChapter.emit();
        this.cdRef.markForCheck();
      }
    }
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
    this.atBottom = false;
    this.checkIfShouldTriggerContinuousReader();
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

  handleBottomIntersection(entries: IntersectionObserverEntry[]) {
    if (entries.length > 0 && this.pageNum > this.totalPages - 5 && this.initFinished) {
      this.debugLog('[Intersection] The whole bottom spacer is visible', entries[0].isIntersecting);
      this.loadNextChapter.emit();
    }
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
    this.pageNumberChange.emit(this.pageNum);
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
