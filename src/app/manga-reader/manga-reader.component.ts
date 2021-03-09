import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import {Location} from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';
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
import { forkJoin } from 'rxjs';

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

    forkJoin({
      chapter: this.seriesService.getChapter(this.chapterId),
      pageNum: this.readerService.getBookmark(this.chapterId),
      chapterPath: this.readerService.getChapterPath(this.chapterId)
    }).subscribe(results => {
      this.chapter = results.chapter;
      this.volumeId = results.chapter.volumeId;
      this.maxPages = results.chapter.pages;

      if (this.pageNum > this.maxPages) {
        this.pageNum = this.maxPages;
      }
      this.pageNum = results.pageNum;

      this.mangaFileName = results.chapterPath;

      this.loadPage();

    }, () => {
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
    this.canvasImage.onload = () => this.renderPage();
  }

  ngOnDestroy() {
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

  prefetch() {
    for (let i = 1; i < PREFETCH_PAGES; i++) {
      const nextPage = this.pageNum + i;
      if (nextPage < this.maxPages) {
        const img = new Image();
        img.src = this.readerService.getPageUrl(this.chapterId, nextPage);
      }
    }
  }

  getPageKey(pageNum: number) {
    return `kavita-page-cache-${this.user.username}-${this.chapterId}--${pageNum}`;
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

  renderPage() {
    if (this.ctx && this.canvas) {
      this.canvas.nativeElement.width = this.canvasImage.width;
      this.canvas.nativeElement.height = this.canvasImage.height;
      const needsSplitting = this.canvasImage.width > this.canvasImage.height;
      this.updateSplitPage();

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
  }

  loadPage() {
    if (!this.canvas || !this.ctx) { return; }

    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).subscribe(() => {}, err => {
      console.error('Could not save bookmark status. Current page is: ', this.pageNum);
    });

    this.isLoading = true;
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
