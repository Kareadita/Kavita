import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, Renderer2, SimpleChanges } from '@angular/core';
import { BehaviorSubject, fromEvent, ReplaySubject, Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { ReaderService } from '../../_services/reader.service';
import { PAGING_DIRECTION } from '../_models/reader-enums';
import { WebtoonImage } from '../_models/webtoon-image';

@Component({
  selector: 'app-infinite-scroller',
  inputs: ['direction'],
  templateUrl: './infinite-scroller.component.html',
  styleUrls: ['./infinite-scroller.component.scss']
})
export class InfiniteScrollerComponent implements OnInit, OnChanges, OnDestroy {

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
  @Input() urlProvider!: (page: number) => string;
  @Output() pageNumberChange: EventEmitter<number> = new EventEmitter<number>();

  @Input() goToPage: ReplaySubject<number> = new ReplaySubject<number>();

  @Input() direction: number = 0;

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
  webtoonImageWidth: number = window.innerWidth || document.documentElement.clientWidth || document.body.clientWidth;
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
   * Debug mode. Will show extra information
   */
  debug: boolean = false;

  get minPageLoaded() {
    return Math.min(...Object.values(this.imagesLoaded));
  }

  get maxPageLoaded() {
    return Math.max(...Object.values(this.imagesLoaded));
  }



  private readonly onDestroy = new Subject<void>();

  constructor(private readerService: ReaderService, private renderer: Renderer2) { }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes.hasOwnProperty('totalPages') && changes['totalPages'].previousValue != changes['totalPages'].currentValue) {
      this.totalPages = changes['totalPages'].currentValue;
      this.initWebtoonReader();
    }
  }

  ngOnDestroy(): void {
    this.intersectionObserver.disconnect();
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit(): void {
    fromEvent(window, 'scroll')
    .pipe(debounceTime(20), takeUntil(this.onDestroy))
    .subscribe((event) => this.handleScrollEvent(event));

    if (this.goToPage) {
      this.goToPage.pipe(takeUntil(this.onDestroy)).subscribe(page => {
        this.debugLog('[GoToPage] jump has occured from ' + this.pageNum + ' to ' + page);
        const isSamePage = this.pageNum === page;
        if (isSamePage) { return; }

        if (this.pageNum < page) {
          this.scrollingDirection = PAGING_DIRECTION.FORWARD;
        } else {
          this.scrollingDirection = PAGING_DIRECTION.BACKWARDS;
        }

        this.setPageNum(page, true);
      });
    }
  }

  /**
   * On scroll in document, calculate if the user/javascript has scrolled to the current image element (and it's visible), update that scrolling has ended completely, 
   * and calculate the direction the scrolling is occuring. This is used for prefetching.
   * @param event Scroll Event
   */
  handleScrollEvent(event?: any) {
    const verticalOffset = this.direction
                          ? (window.pageXOffset || document.getElementsByClassName('horizontal')[0].scrollLeft || document.body.scrollLeft|| 0)
                          : (window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0);

    if (this.isScrolling && this.currentPageElem != null && this.isElementVisible(this.currentPageElem)) {
      this.debugLog('[Scroll] Image is visible from scroll, isScrolling is now false');
      this.isScrolling = false;
    }

    if (verticalOffset > this.prevScrollPosition) {
      this.scrollingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.scrollingDirection = PAGING_DIRECTION.BACKWARDS;
    }
    this.prevScrollPosition = verticalOffset;
  }

  /**
   * Is any part of the element visible in the scrollport. Does not take into account 
   * style properites, just scroll port visibility. 
   * @param elem 
   * @returns 
   */
  isElementVisible(elem: Element) {
    if (elem === null || elem === undefined) { return false; }

    // NOTE: This will say an element is visible if it is 1 px offscreen on top
    var rect = elem.getBoundingClientRect();

    return (rect.bottom >= 0 && 
            rect.right >= 0 && 
            rect.top <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.left <= (window.innerWidth || document.documentElement.clientWidth)
          );
  }


  initWebtoonReader() {
    this.imagesLoaded = {};
    this.webtoonImages.next([]);

    const [startingIndex, endingIndex] = this.calculatePrefetchIndecies();

    this.debugLog('[INIT] Prefetching pages ' + startingIndex + ' to ' + endingIndex + '. Current page: ', this.pageNum);
    for(let i = startingIndex; i <= endingIndex; i++) {
      this.loadWebtoonImage(i);
    }
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

    if (!this.direction) { // WEBTOON
      this.renderer.setAttribute(event.target, 'width', this.webtoonImageWidth + '');
      this.renderer.setAttribute(event.target, 'height', event.target.height + '');
      this.renderer.setAttribute(event.target, 'display', 'block;'+ '');
    } else { // HORIZONTAL
      this.renderer.setAttribute(event.target, 'height', document.documentElement.clientHeight + 'px' + '');
      this.renderer.setAttribute(event.target, 'float', 'left' + '');
      this.renderer.setAttribute(event.target, 'display', 'inline-block;'+ '');
    }

    this.attachIntersectionObserverElem(event.target);

    if (imagePage === this.pageNum) {
      Promise.all(Array.from(document.querySelectorAll('img'))
        .filter((img: any) => !img.complete)
        .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
        .then(() => {
          this.debugLog('[Image Load] ! Loaded current page !', this.pageNum);
          this.currentPageElem = document.querySelector('img#page-' + this.pageNum);
          
          if (this.currentPageElem && !this.isElementVisible(this.currentPageElem)) { 
            this.scrollToCurrentPage();
          }
          
          this.allImagesLoaded = true;
      });
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
        this.setPageNum(imagePage);
      }
    });
  }

  /**
   * Set the page number, invoke prefetching and optionally scroll to the new page.
   * @param pageNum Page number to set to. Will trigger the pageNumberChange event emitter.
   * @param scrollToPage Optional (default false) parameter to trigger scrolling to the newly set page
   */
  setPageNum(pageNum: number, scrollToPage: boolean = false) {
    this.pageNum = pageNum;
    this.pageNumberChange.emit(this.pageNum);

    this.prefetchWebtoonImages();

    if (scrollToPage) {
      const currentImage = document.querySelector('img#page-' + this.pageNum);
      if (currentImage !== null && !this.isElementVisible(currentImage)) {
        this.debugLog('[GoToPage] Scrolling to page', this.pageNum);
        this.scrollToCurrentPage();
      }
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
    
    // Update prevScrollPosition, so the next scroll event properly calculates direction
    this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
    this.isScrolling = true;

    setTimeout(() => {
      if (this.currentPageElem) {
        this.debugLog('[Scroll] Scrolling to page ', this.pageNum);
        this.currentPageElem.scrollIntoView({behavior: 'smooth'});
      }
    }, 600);
  }

  loadWebtoonImage(page: number) {
    let data = this.webtoonImages.value;

    if (this.imagesLoaded.hasOwnProperty(page)) {
      this.debugLog('\t[PREFETCH] Skipping prefetch of ', page);
      return;
    }
    this.debugLog('\t[PREFETCH] Prefetching ', page);

    data = data.concat({src: this.urlProvider(page), page});

    data.sort((a: WebtoonImage, b: WebtoonImage) => {
      if (a.page < b.page) { return -1; }
      else if (a.page > b.page) { return 1; }
      else return 0;
    });

    this.allImagesLoaded = false;
    this.webtoonImages.next(data);

    if (!this.imagesLoaded.hasOwnProperty(page)) {
      this.imagesLoaded[page] = page;
    }
  }

  attachIntersectionObserverElem(elem: HTMLImageElement) {
    if (elem !== null) {
      this.intersectionObserver.observe(elem);
      this.debugLog('Attached Intersection Observer to page', this.readerService.imageUrlToPageNum(elem.src));
    } else {
      console.error('Could not attach observer on elem'); // This never happens
    }
  }

  calculatePrefetchIndecies() {
    let startingIndex = 0;
    let endingIndex = 0;
    if (this.isScrollingForwards()) {
      startingIndex = Math.min(Math.max(this.pageNum - this.bufferPages, 0), this.totalPages);
      endingIndex = Math.min(Math.max(this.pageNum + this.bufferPages, 0), this.totalPages);

      if (startingIndex === this.totalPages) {
        return [0, 0];
      }
    } else {
      startingIndex = Math.min(Math.max(this.pageNum - this.bufferPages, 0), this.totalPages);
      endingIndex = Math.min(Math.max(this.pageNum + this.bufferPages, 0), this.totalPages);
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

  prefetchWebtoonImages() {
    const [startingIndex, endingIndex] = this.calculatePrefetchIndecies();
    if (startingIndex === 0 && endingIndex === 0) { return; }

    this.debugLog('\t[PREFETCH] prefetching pages: ' + startingIndex + ' to ' + endingIndex);
    for(let i = startingIndex; i < endingIndex; i++) {
      this.loadWebtoonImage(i);
    }

    Promise.all(Array.from(document.querySelectorAll('img'))
      .filter((img: any) => !img.complete)
      .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
      .then(() => {
        this.allImagesLoaded = true;
    });
  }

  debugLog(message: string, extraData?: any) {
    if (!this.debug) { return; }

    if (extraData !== undefined) {
      console.log(message, extraData);  
    } else {
      console.log(message);
    }
  }

  h_scroll(event: WheelEvent): void {
    if (this.direction) {
      document.getElementsByClassName('horizontal')[0].scrollLeft -= event.deltaY;
      event.preventDefault();
      this.handleScrollEvent();
    }
  }
}
