import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, Renderer2, SimpleChanges } from '@angular/core';
import { BehaviorSubject, fromEvent, Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { CircularArray } from 'src/app/shared/data-structures/circular-array';
import { ReaderService } from 'src/app/_services/reader.service';
import { PAGING_DIRECTION } from '../_models/reader-enums';
import { WebtoonImage } from '../_models/webtoon-image';

@Component({
  selector: 'app-infinite-scroller',
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
  @Input() buffferPages: number = 5;
  /**
   * Total number of pages
   */
  @Input() totalPages: number = 0;
  /**
   * Method to generate the src for Image loading
   */
  @Input() urlProvider!: (page: number) => string;
  @Output() pageNumberChange: EventEmitter<number> = new EventEmitter<number>();
  
  /**
   * Stores and emits all the src urls
   */
  webtoonImages: BehaviorSubject<WebtoonImage[]> = new BehaviorSubject<WebtoonImage[]>([]);

  /**
   * Responsible for calculating current page on screen and uses hooks to trigger prefetching.
   * Note: threshold will fire differently due to size of images. 1 requires full image on screen.
   */
  intersectionObserver: IntersectionObserver = new IntersectionObserver((entries) => this.handleIntersection(entries), { threshold: [0] });
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
   * The min page number that has been prefetched
   */
  minPrefetchedWebtoonImage: number = Number.MAX_SAFE_INTEGER;
  /**
   * The max page number that has been prefetched
   */
  maxPrefetchedWebtoonImage: number = Number.MIN_SAFE_INTEGER;
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
   * Debug mode. Will show extra information
   */
  debug: boolean = false;

  /**
   * Timer to help detect when a scroll end event has occured  (not used)
   */
  scrollEndTimer: any;


  /**
   * Each pages height mapped to page number as key (not used)
   */
  pageHeights:{[key: number]: number} = {};

  buffer: CircularArray<HTMLImageElement> = new CircularArray<HTMLImageElement>([], 0);



  private readonly onDestroy = new Subject<void>();

  constructor(private readerService: ReaderService, private renderer: Renderer2) { }

  ngOnChanges(changes: SimpleChanges): void {
    let shouldInit = false;
    console.log('Changes: ', changes);
    if (changes.hasOwnProperty('totalPages') && changes['totalPages'].currentValue === 0) {
      this.debugLog('Swallowing variable change due to totalPages being 0');
      return;
    }

    if (changes.hasOwnProperty('totalPages') && changes['totalPages'].previousValue != changes['totalPages'].currentValue) {
      this.totalPages = changes['totalPages'].currentValue;
      shouldInit = true;
    }
    if (changes.hasOwnProperty('pageNum') && changes['pageNum'].previousValue != changes['pageNum'].currentValue) {
      // Manually update pageNum as we are getting notified from a parent component, hence we shouldn't invoke update
      this.setPageNum(changes['pageNum'].currentValue);
      if (Math.abs(changes['pageNum'].currentValue - changes['pageNum'].previousValue) > 2) {
        // Go to page has occured
        shouldInit = true;
      }
    }

    if (shouldInit) {
      this.initWebtoonReader();
    }

    // This should only execute on initial load or from a gotopage update
    const currentImage = document.querySelector('img#page-' + this.pageNum);
    if (currentImage !== null && this.isElementVisible(currentImage)) {

      if ((changes.hasOwnProperty('pageNum') && Math.abs(changes['pageNum'].previousValue - changes['pageNum'].currentValue) <= 0) || !shouldInit) {
        return;
      }
      this.debugLog('Scrolling to page', this.pageNum);
      this.scrollToCurrentPage();
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
  }

  handleScrollEvent(event?: any) {
    const verticalOffset = (window.pageYOffset 
      || document.documentElement.scrollTop 
      || document.body.scrollTop || 0);


    clearTimeout(this.scrollEndTimer);
    this.scrollEndTimer = setTimeout(() => this.handleScrollEnd(), 150);

    if (this.debug && this.isScrolling) {
      this.debugLog('verticalOffset: ', verticalOffset);
      this.debugLog('scroll to element offset: ', this.currentPageElem?.getBoundingClientRect().top);
    }

    if (this.currentPageElem != null && this.isElementVisible(this.currentPageElem)) {
      this.debugLog('Image is visible from scroll, isScrolling is now false');
      this.isScrolling = false;
    }

    if (verticalOffset > this.prevScrollPosition) {
      this.scrollingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.scrollingDirection = PAGING_DIRECTION.BACKWARDS;
    }
    this.prevScrollPosition = verticalOffset;
  }

  // ! This will fire twice from an automatic scroll
  handleScrollEnd() {
    //console.log('!!! Scroll End Event !!!');
  }


  /**
   * Is any part of the element visible in the scrollport. Does not take into account 
   * style properites, just scroll port visibility. 
   * Note: use && to ensure the whole images is visible
   * @param elem 
   * @returns 
   */
  isElementVisible(elem: Element) {
    if (elem === null || elem === undefined) { return false; }

    const docViewTop = window.pageYOffset;
    const docViewBottom = docViewTop + window.innerHeight;

    const elemTop = elem.getBoundingClientRect().top;
    const elemBottom = elemTop + elem.getBoundingClientRect().height;

    return ((elemBottom <= docViewBottom) || (elemTop >= docViewTop));
  }


  initWebtoonReader() {

    this.minPrefetchedWebtoonImage = this.pageNum;
    this.maxPrefetchedWebtoonImage = Number.MIN_SAFE_INTEGER;
    this.webtoonImages.next([]);

    


    const prefetchStart = Math.max(this.pageNum - this.buffferPages, 0);
    const prefetchMax =  Math.min(this.pageNum + this.buffferPages, this.totalPages); 
    this.debugLog('[INIT] Prefetching pages ' + prefetchStart + ' to ' + prefetchMax + '. Current page: ', this.pageNum);
    for(let i = prefetchStart; i < prefetchMax; i++) {
      this.prefetchWebtoonImage(i);
    }

    const images = [];
    for (let i = prefetchStart; i < prefetchMax; i++) {
      images.push(new Image());
    }
    this.buffer = new CircularArray<HTMLImageElement>(images, this.buffferPages);
    
    this.minPrefetchedWebtoonImage = prefetchStart;
    this.maxPrefetchedWebtoonImage = prefetchMax;

    this.debugLog('Buffer: ', this.buffer.arr.map(img => this.readerService.imageUrlToPageNum(img.src)));
  }

  /**
   * Callback for an image onLoad. At this point the image is already rendered in DOM (may not be visible)
   * This will be used to scroll to current page for intial load
   * @param event 
   */
  onImageLoad(event: any) {
    const imagePage = this.readerService.imageUrlToPageNum(event.target.src);
    this.debugLog('Image loaded: ', imagePage);


    if (event.target.width < this.webtoonImageWidth) {
      this.webtoonImageWidth = event.target.width;
    }

    this.pageHeights[imagePage] = event.target.getBoundingClientRect().height;

    this.renderer.setAttribute(event.target, 'width', this.webtoonImageWidth + '');
    this.renderer.setAttribute(event.target, 'height', event.target.height + '');

    this.attachIntersectionObserverElem(event.target);

    if (imagePage === this.pageNum) {
      Promise.all(Array.from(document.querySelectorAll('img'))
        .filter((img: any) => !img.complete)
        .map((img: any) => new Promise(resolve => { img.onload = img.onerror = resolve; })))
        .then(() => {
          this.debugLog('! Loaded current page !', this.pageNum);
          this.allImagesLoaded = true;
      });
    }
  }

  handleIntersection(entries: IntersectionObserverEntry[]) {

    if (!this.allImagesLoaded || this.isScrolling) {
      this.debugLog('Images are not loaded (or performing scrolling action), skipping any scroll calculations');
      return;
    }


    entries.forEach(entry => {
      const imagePage = parseInt(entry.target.attributes.getNamedItem('page')?.value + '', 10);
      this.debugLog('[Intersection] Page ' + imagePage + ' is visible: ', entry.isIntersecting);
      if (entry.isIntersecting) {
        this.debugLog('[Intersection] ! Page ' + imagePage + ' just entered screen');
        this.setPageNum(imagePage);
        this.prefetchWebtoonImages();
      }
    });
  }

  setPageNum(pageNum: number) {
    this.pageNum = pageNum;
    this.pageNumberChange.emit(this.pageNum);

    //TODO: Perform check here to see if prefetching or DOM removal is needed
  }

  isScrollingForwards() {
    return this.scrollingDirection === PAGING_DIRECTION.FORWARD;
  }

  scrollToCurrentPage() {
    this.currentPageElem = document.querySelector('img#page-' + this.pageNum);
    if (!this.currentPageElem) { return; }
    // Update prevScrollPosition, so the next scroll event properly calculates direction
    this.prevScrollPosition = this.currentPageElem.getBoundingClientRect().top;
    this.isScrolling = true;

    setTimeout(() => {
      if (this.currentPageElem) {
        this.currentPageElem.scrollIntoView({behavior: 'smooth'});
      }
    }, 600);
  }

  prefetchWebtoonImage(page: number) {
    let data = this.webtoonImages.value;

    data = data.concat({src: this.urlProvider(page), page});

    data.sort((a: WebtoonImage, b: WebtoonImage) => {
      if (a.page < b.page) { return -1; }
      else if (a.page > b.page) { return 1; }
      else return 0;
    });

    if (page < this.minPrefetchedWebtoonImage) {
      this.minPrefetchedWebtoonImage = page;
    }
    if (page > this.maxPrefetchedWebtoonImage) {
      this.maxPrefetchedWebtoonImage = page;
    }
    this.allImagesLoaded = false;

    this.webtoonImages.next(data);

    let index = 1;

    this.buffer.applyFor((item, i) => {
      const offsetIndex = this.pageNum + index;
      const urlPageNum = this.readerService.imageUrlToPageNum(item.src);
      if (urlPageNum === offsetIndex) {
        index += 1;
        return;
      }
      if (offsetIndex < this.totalPages - 1) {
        item.src = this.urlProvider(offsetIndex);
        index += 1;
      }
    }, this.buffer.size() - 3);


  }

  attachIntersectionObserverElem(elem: HTMLImageElement) {
    if (elem !== null) {
      this.intersectionObserver.observe(elem);
      this.debugLog('Attached Intersection Observer to page', this.readerService.imageUrlToPageNum(elem.src));
    } else {
      console.error('Could not attach observer on elem');
    }
  }

  prefetchWebtoonImages() {
    let startingIndex = 0;
    let endingIndex = 0;
    if (this.isScrollingForwards()) {
      startingIndex = Math.min(this.maxPrefetchedWebtoonImage + 1, this.totalPages);
      endingIndex = Math.min(this.maxPrefetchedWebtoonImage + 1 + this.buffferPages, this.totalPages); 

      if (startingIndex === this.totalPages) {
        return;
      }
    } else {
      startingIndex = Math.max(this.minPrefetchedWebtoonImage - 1, 0) ;
      endingIndex = Math.max(this.minPrefetchedWebtoonImage - 1 - this.buffferPages, 0);

      if (startingIndex <= 0) {
        return;
      }
    }


    if (startingIndex > endingIndex) {
      const temp = startingIndex;
      startingIndex = endingIndex;
      endingIndex = temp;
    }
    this.debugLog('[Prefetch] prefetching pages: ' + startingIndex + ' to ' + endingIndex);
    this.debugLog('     [Prefetch] page num: ', this.pageNum);
    // If a request comes in to prefetch over current page +/- bufferPages (+ 1 due to requesting from next/prev page), then deny it
    this.debugLog('     [Prefetch] Caps: ' + (this.pageNum - (this.buffferPages + 1)) + ' - ' + (this.pageNum + (this.buffferPages + 1)));
    if (this.isScrollingForwards() && startingIndex > this.pageNum + (this.buffferPages + 1)) {
      this.debugLog('[Prefetch] A request that is too far outside buffer range has been declined', this.pageNum);
      return;
    }
    if (!this.isScrollingForwards() && endingIndex < (this.pageNum - (this.buffferPages + 1))) {
      this.debugLog('[Prefetch] A request that is too far outside buffer range has been declined', this.pageNum);
      return;
    }
    for(let i = startingIndex; i < endingIndex; i++) {
      this.prefetchWebtoonImage(i);
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
}
