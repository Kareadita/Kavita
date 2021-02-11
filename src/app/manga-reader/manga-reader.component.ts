import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import {Location} from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MangaImage } from '../_models/manga-image';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';
import { FormBuilder, FormGroup } from '@angular/forms';
import { NavService } from '../_services/nav.service';
import { Chapter } from '../_models/chapter';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { PageSplitOption } from '../_models/preferences/page-split-option';

enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft',
  ESC_KEY = 'Escape',
  SPACE = ' '
}

// export enum READING_DIRECTION {
//   LEFT_TO_RIGHT = 1,
//   RIGHT_TO_LEFT = 2
// }

enum FITTING_OPTION {
  HEIGHT = 'full-height',
  WIDTH = 'full-width',
  ORIGINAL = 'original'
}

const MAX_CACHED_PAGES = 5;

export class Queue<T> {
  elements: T[];

  constructor() {
    this.elements = [];
  }

  enqueue(data: T) {
    this.elements.push(data);
  }

  dequeue() {
    return this.elements.shift();
  }

  isEmpty() {
    return this.elements.length === 0;
  }

  peek() {
    return !this.isEmpty() ? this.elements[0] : undefined;
  }

  length = () => {
    return this.elements.length;
  }
}

@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss']
})
export class MangaReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  // Is there a way to turn off the nav bar? Perhaps a service?
  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;

  minWidth = 99999; // Min width we see, so that we can tell if a page needs splitting
  currentImage: any;
  pageNum = 0;
  maxPages = 1;
  user!: User;
  fittingForm: FormGroup | undefined;
  splitForm: FormGroup | undefined;

  readingDirection = ReadingDirection.LeftToRight;
  scalingOption = ScalingOption.FitToHeight;
  pageSplitOption = PageSplitOption.SplitRightToLeft;


  images: MangaImage[] = [];
  cachedImages = new Queue<MangaImage>();
  cachedPages = new Queue<number>();
  image: MangaImage | undefined; // Used soley to display information on UI. Not used for manipulation

  menuOpen = false;
  isLoading = true; // we need to debounce this so it only kicks in longer than 30 ms load time

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;

  // Temp hack: Override background color for reader and restore it onDestroy
  originalBodyColor: string | undefined;


  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
              private seriesService: SeriesService, private readerService: ReaderService, private location: Location,
              private formBuilder: FormBuilder, private navService: NavService) {
                this.navService.hideNavBar();
  }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');

    if (libraryId === null || seriesId === null || chapterId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.setOverrideStyles();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.readingDirection = this.user.preferences.readingDirection;
        this.scalingOption = this.user.preferences.scalingOption;
        this.pageSplitOption = this.user.preferences.pageSplitOption;
        this.fittingForm = this.formBuilder.group({
          fittingOption: this.translateScalingOption(this.scalingOption)
        });
        this.splitForm = this.formBuilder.group({
          pageSplitOption: this.pageSplitOption + ''
        });
      }
    });

    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);


    this.seriesService.getChapter(this.chapterId).subscribe(chapter => {
      this.chapter = chapter;
      this.volumeId = chapter.volumeId;
      this.maxPages = chapter.pages;
      this.readerService.getBookmark(this.chapterId).subscribe(pageNum => {
        if (this.pageNum > this.maxPages) {
          this.pageNum = this.maxPages;
        }
        this.pageNum = pageNum;
        this.loadPage();
      });
    }, err => {
      setTimeout(() => {
        this.location.back();
      }, 200);
    });

  }

  ngAfterViewInit() {
    if (!this.canvas) {
      return;
    }
    this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });

  }

  ngOnDestroy() {
    for (let i = 0; i < this.maxPages; i++) {
      localStorage.removeItem(this.getPageKey(i));
    }

    // NOTE: Should I remove all other keys for page cache? What about using a timestamp on when to clear out old cache entries? 
    //Object.entries(localStorage)

    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null && this.originalBodyColor !== undefined) {
      bodyNode.style.background = this.originalBodyColor;
      bodyNode.style.height = '100%';
    }
    this.navService.showNavBar();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
    } else if (event.key === KEY_CODES.ESC_KEY) {
      this.location.back();
    } else if (event.key === KEY_CODES.SPACE) {
      this.toggleMenu();
    }
  }

  setOverrideStyles() {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null) {
      this.originalBodyColor = bodyNode.style.background;
      bodyNode.style.background = 'black';
      bodyNode.style.height = '0%';
    }
  }

  translateScalingOption(option: ScalingOption) {
    switch (option) {
      case (ScalingOption.FitToHeight):
        return FITTING_OPTION.HEIGHT;
      case (ScalingOption.FitToWidth):
        return FITTING_OPTION.WIDTH;
      default:
        return FITTING_OPTION.ORIGINAL;
    }
  }

  getFittingOptionClass() {
    if (this.fittingForm === undefined) {
      return FITTING_OPTION.HEIGHT;
    }
    return this.fittingForm.value.fittingOption;
  }

  toggleMenu() {
    this.menuOpen = !this.menuOpen;
  }

  /**
   * @description Checks the cache interface if this page is cached.
   * @param pageNum Page number to check against
   * @return The MangaImage or undefined if it doesn't exist
   */
  checkCache(pageNum: number): MangaImage | undefined {
    const key = this.getPageKey(pageNum);
    const image = localStorage.getItem(key);
    if (image !== null) {
      return JSON.parse(image);
    }
    return undefined;
  }

  // I might want to debounce this and move to the nextPage code so it doesn't execute if we are spamming next page (although i don't see any issues)
  prefetch() {
    const nextPage = this.pageNum + 1;
    if (this.isCached(nextPage)) {
      console.log('Page ' + nextPage + ' is already cached.');
      return;
    }
    if (nextPage < this.maxPages) {
      console.log('Prefetching next page: ', nextPage);
      this.readerService.getPage(this.chapterId, nextPage).subscribe(image => {
        this.cache(image, nextPage);
      });
    }
  }

  isCached(pageNum: number) {
    const key = this.getPageKey(pageNum);
    return (localStorage.getItem(key) !== null);
  }

  cache(image: MangaImage, pageNum: number) {
    this.cachedPages.enqueue(pageNum);
    this.cachedImages.enqueue(image);

    try {
      const key = this.getPageKey(pageNum);
      if (!this.isCached(pageNum)) {
        localStorage.setItem(key, JSON.stringify(image));
      }
    } catch (error) {
      const pageToRemove = this.cachedPages.dequeue();
      if (pageToRemove === undefined) {
        return;
      }
      const key = this.getPageKey(pageToRemove);
      if (localStorage.getItem(key) !== null) {
        localStorage.removeItem(key);
        this.cache(image, pageNum); // We re-call cache so that the current page does get cached
      }
    }

  }

  clearCanvas() {
    if (!this.canvas || !this.ctx) {
      return;
    }

    this.ctx.fillStyle = '#000';
    this.ctx.fillRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
  }

  getPageKey(pageNum: number) {
    return `kavita-page-cache-${this.user.username}-${this.chapterId}--${pageNum}`;
  }

  renderPage(image: MangaImage) {
    if (!this.canvas || !this.ctx) {
      return;
    }
    this.isLoading = false;
    this.image = image;
    this.clearCanvas();
    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).subscribe(() => {}, err => {
      console.error('Could not save bookmark status. Current page is: ', this.pageNum);
    });

    if (image.width < this.minWidth) {
      this.minWidth = image.width;
    }
    this.currentImage = image;

    this.canvas.nativeElement.width = image.width;
    this.canvas.nativeElement.height = image.height;

    this.cache(image, this.pageNum);
    this.prefetch();

    const that = this;
    const img = new Image();
    img.onload = () => {
      if (that.ctx) {
        that.ctx.drawImage(img, 0, 0);
      }
    };

    img.src = 'data:image/jpeg;base64,' + image.content;
  }

  handlePageChange(event: any, direction: string) {
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
    if (this.pageNum + 1 >= this.maxPages) {
      // TODO: Ask if they want to load next chapter/volume
      return;
    }

    // TODO: Check if this is a split page

    this.pageNum++;
    this.loadPage();
  }

  prevPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.pageNum - 1 < 0) {
      return;
    }
    this.pageNum--;
    this.loadPage();
  }

  loadPage() {
    this.isLoading = true;

    // Check cache if we already have this page
    const existingImage = this.checkCache(this.pageNum);
    if (existingImage) {
      console.log('Using a cached image');
      this.renderPage(existingImage);
    } else {
      this.readerService.getPage(this.chapterId, this.pageNum).subscribe(image => {
        this.renderPage(image);
      });
    }
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }
  }

  goToPage() {
    const goToPageNum = window.prompt('What page would you like to go to?', '');
    if (goToPageNum === null) { return; }

    let page = parseInt(goToPageNum, 10);

    if (page >= this.maxPages) {
      page = this.maxPages - 1;
    } else if (page < 0) {
      page = 0;
    }

    this.pageNum = page;
    this.loadPage();

  }

}
