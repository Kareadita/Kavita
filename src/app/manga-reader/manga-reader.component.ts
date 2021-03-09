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

const PREFETCH_PAGES = 5;

enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft',
  ESC_KEY = 'Escape',
  SPACE = ' '
}

enum FITTING_OPTION {
  HEIGHT = 'full-height',
  WIDTH = 'full-width',
  ORIGINAL = 'original'
}

enum SPLIT_PAGE_PART {
  NO_SPLIT = 'none',
  LEFT_PART = 'left',
  RIGHT_PART = 'right'
}

enum PAGING_DIRECTION {
  FORWARD = 1,
  BACKWARDS = -1,
}

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
  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;

  pageNum = 0;
  maxPages = 1;
  user!: User;
  fittingForm: FormGroup | undefined;
  splitForm: FormGroup | undefined;

  readingDirection = ReadingDirection.LeftToRight;
  scalingOption = ScalingOption.FitToHeight;
  pageSplitOption = PageSplitOption.SplitRightToLeft;

  cachedPages = new Queue<number>();
  image: MangaImage | undefined; // Used soley to display information on UI. Not used for manipulation
  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;

  menuOpen = false;
  isLoading = true; // we need to debounce this so it only kicks in longer than 30 ms load time
  mangaFileName = '';

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private canvasImage = new Image();

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

        // Clear out cache entries that are less than currentPage - 1
        const cuttoff = this.pageNum - 1;
        Object.entries(localStorage).filter(entry => entry[0].startsWith('kavita-page-cache-' + this.user.username + '-' + this.chapterId))
          .filter(entry => parseInt(entry[0].replace('kavita-page-cache-' + this.user.username + '-' + this.chapterId + '--', ''), 10) < cuttoff)
          .forEach(entry => {
            localStorage.removeItem(entry[0]);
        });

        this.readerService.getChapterPath(this.chapterId).subscribe((path: string) => {
          this.mangaFileName = path;
          console.log('manga File Name: ', path);
        });

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
    this.canvasImage.onload = () => {
      if (this.ctx && this.canvas) {
        this.canvas.nativeElement.width = this.canvasImage.width;
        this.canvas.nativeElement.height = this.canvasImage.height;
        const needsSplitting = this.canvasImage.width > this.canvasImage.height;
        this.updateSplitPage(); // TODO: This is broken

        if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
          this.canvas.nativeElement.width = this.canvasImage.width / 2;
          this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
        } else if (needsSplitting && this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
          this.canvas.nativeElement.width = this.canvasImage.width / 2;
          this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
        } else {
          this.ctx.drawImage(this.canvasImage, 0, 0);
        }
      }
      this.isLoading = false;
    };
  }

  ngOnDestroy() {
    for (let i = 0; i < this.maxPages; i++) {
      localStorage.removeItem(this.getPageKey(i));
    }

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


  prefetch() {
    // this code works, but not consistently and without as much control as I'd like.
    const cachedPages: any = {};
    this.cachedPages.elements.forEach((num: number) => {
      cachedPages[num] = num;
    });
    for (let i = 1; i < PREFETCH_PAGES; i++) {
      const nextPage = this.pageNum + i;
      if (cachedPages.hasOwnProperty(nextPage)) {
        continue;
      }
      if (nextPage < this.maxPages) {
        // this.readerService.getPageInfo(this.chapterId, nextPage).subscribe(image => {
        //   this.cache(image, nextPage);
        // });

        const img = new Image();
        img.src = this.readerService.getPageUrl(this.chapterId, nextPage);
      }
    }
  }

  isCached(pageNum: number) {
    return this.cachedPages.elements.filter(page => page === pageNum).length > 0;
  }

  cache(image: MangaImage, pageNum: number) {
    this.cachedPages.enqueue(pageNum);

    if (!image.contentUrl) {
      image.contentUrl = this.readerService.getPageUrl(this.chapterId, pageNum);
    }

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

  getPageKey(pageNum: number) {
    return `kavita-page-cache-${this.user.username}-${this.chapterId}--${pageNum}`;
  }

  renderPage(image: MangaImage) {
    if (!this.canvas || !this.ctx) {
      return;
    }
    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).subscribe(() => {}, err => {
      console.error('Could not save bookmark status. Current page is: ', this.pageNum);
    });


    this.image = image;
    // this.canvas.nativeElement.width = image.width;
    // this.canvas.nativeElement.height = image.height;
    //this.canvas.nativeElement.fillStyle = 'black';

    //this.cache(image, this.pageNum);
    //this.updateSplitPage(); 

    this.canvasImage.src = this.readerService.getPageUrl(this.chapterId, this.pageNum);
    this.prefetch();
  }

  isSplitLeftToRight() {
    return (this.splitForm?.get('pageSplitOption')?.value + '') === (PageSplitOption.SplitLeftToRight + '');
  }

  isNoSplit() {
    return (this.splitForm?.get('pageSplitOption')?.value + '') === (PageSplitOption.NoSplit + '');
  }

  updateSplitPage() {
    const needsSplitting = this.canvasImage.width > this.canvasImage.height;
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
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.NO_SPLIT : SPLIT_PAGE_PART.LEFT_PART;
          break;
      }
    } else if (this.pagingDirection === PAGING_DIRECTION.BACKWARDS) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.NO_SPLIT : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT;
          break;
      }
    }
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
      if (confirm('Do you want to load the next Volume/Chapter?')) {

      }
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    if (this.isNoSplit() || this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART)) {
      this.pageNum++;
    }

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

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    if (this.isNoSplit() || this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART)) {
      this.pageNum--;
    }

    this.loadPage();
  }

  loadPage() {
    this.isLoading = true;

    if (!this.canvas || !this.ctx) {
      return;
    }
    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).subscribe(() => {}, err => {
      console.error('Could not save bookmark status. Current page is: ', this.pageNum);
    });


    //this.canvas.nativeElement.width = this.canvasImage.width;
    //this.canvas.nativeElement.height = this.canvasImage.height;
    //this.canvas.nativeElement.fillStyle = 'black';

    //this.cache(image, this.pageNum);
    //this.updateSplitPage();

    this.canvasImage.src = this.readerService.getPageUrl(this.chapterId, this.pageNum);
    this.prefetch();
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }
  }

  promptForPage() {
    const goToPageNum = window.prompt('What page would you like to go to?', '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  goToPage(pageNum?: number) {
    let page = pageNum;
    if (pageNum === null || pageNum === undefined) {
      const goToPageNum = this.promptForPage();
      if (goToPageNum === null) { return; }
      page = parseInt(goToPageNum.trim(), 10);
    }

    if (page === undefined) { return; }

    if (page >= this.maxPages) {
      page = this.maxPages - 1;
    } else if (page < 0) {
      page = 0;
    }

    this.pageNum = page;
    this.loadPage();

  }

}
