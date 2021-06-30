import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, SimpleChanges, ViewChild } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { take, takeUntil } from 'rxjs/operators';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ReaderService } from '../_services/reader.service';
import { FormBuilder, FormGroup } from '@angular/forms';
import { NavService } from '../_services/nav.service';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { PageSplitOption } from '../_models/preferences/page-split-option';
import { forkJoin, Subject } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { KEY_CODES } from '../shared/_services/utility.service';
import { CircularArray } from '../shared/data-structures/circular-array';
import { MemberService } from '../_services/member.service';
import { Stack } from '../shared/data-structures/stack';
import { ChangeContext, LabelType, Options } from '@angular-slider/ngx-slider';
import { trigger, state, style, transition, animate } from '@angular/animations';
import { ChapterInfo } from './_models/chapter-info';
import { COLOR_FILTER, FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART } from './_models/reader-enums';
import { Preferences, scalingOptions } from '../_models/preferences/preferences';
import { READER_MODE } from '../_models/preferences/reader-mode';

const PREFETCH_PAGES = 5;

const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

const ANIMATION_SPEED = 200;
const OVERLAY_AUTO_CLOSE_TIME = 6000;
const CLICK_OVERLAY_TIMEOUT = 3000;


@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss'],
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
  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;

  pageNum = 0;
  maxPages = 1;
  user!: User;
  generalSettingsForm!: FormGroup;

  scalingOptions = scalingOptions;
  readingDirection = ReadingDirection.LeftToRight;
  scalingOption = ScalingOption.FitToHeight;
  pageSplitOption = PageSplitOption.SplitRightToLeft;
  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  pagingDirection: PAGING_DIRECTION = PAGING_DIRECTION.FORWARD;
  colorMode: COLOR_FILTER = COLOR_FILTER.NONE;
  autoCloseMenu: boolean = true;
  
  isLoading = true; 

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;
  private canvasImage = new Image();

  cachedImages!: CircularArray<HTMLImageElement>; // This is a circular array of size PREFETCH_PAGES + 2
  continuousChaptersStack: Stack<number> = new Stack();
  

  // Menu overlay variables
  menuOpen = false;
  prevPageDisabled = false;
  nextPageDisabled = false;
  pageOptions: Options = {
    floor: 1,
    ceil: 0,
    step: 1,
    showSelectionBar: true,
    translate: (value: number, label: LabelType) => {
      if (value === 1 || value === this.maxPages) {
        return value + '';
      }
      return (value + 1) + '';
    },
    animate: false
  };

  title: string = '';
  subtitle: string = '';
  /**
   * Timeout id for auto-closing menu overlay
   */
   menuTimeout: any;

   /**
    * Whether the click areas show
    */
   showClickOverlay: boolean = false;

  

  // These are not garunteed to be valid ChapterIds. Prefetch them on page load (non-blocking).
  nextChapterId: number = CHAPTER_ID_NOT_FETCHED;
  prevChapterId: number = CHAPTER_ID_NOT_FETCHED;
  // Used to keep track of if you can move to the next/prev chapter
  nextChapterDisabled: boolean = false;
  prevChapterDisabled: boolean = false;
  nextChapterPrefetched: boolean = false;

  // Whether extended settings area is showing
  settingsOpen: boolean = false;

  readerMode: READER_MODE = READER_MODE.MANGA_LR;
  getPageUrl = (pageNum: number) => this.readerService.getPageUrl(this.chapterId, pageNum);

  

  private readonly onDestroy = new Subject<void>();

  get splitIconClass() {
    if (this.isSplitLeftToRight()) {
      return 'left-side';
    } else if (this.isNoSplit()) {
      return 'none';  
    }
    return 'right-side';
  }

  get readerModeIcon() {
    switch(this.readerMode) {
      case READER_MODE.MANGA_LR:
        return 'fa-exchange-alt';
      case READER_MODE.MANGA_UD:
        return 'fa-exchange-alt fa-rotate-90';
      case READER_MODE.WEBTOON:
        return 'fa-arrows-alt-v';
    }
  }

  get colorOptionName() {
    switch(this.colorMode) {
      case COLOR_FILTER.NONE:
        return 'None';
      case COLOR_FILTER.DARK:
        return 'Dark';
      case COLOR_FILTER.SEPIA:
        return 'Sepia';
    }
  }

  get READER_MODE(): typeof READER_MODE {
    return READER_MODE;
  }

  get ReadingDirection(): typeof ReadingDirection {
    return ReadingDirection;
  }

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
              public readerService: ReaderService, private location: Location,
              private formBuilder: FormBuilder, private navService: NavService, 
              private toastr: ToastrService, private memberService: MemberService) {
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

    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);

    this.continuousChaptersStack.push(this.chapterId);

    this.readerService.setOverrideStyles();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.readingDirection = this.user.preferences.readingDirection;
        this.scalingOption = this.user.preferences.scalingOption;
        this.pageSplitOption = this.user.preferences.pageSplitOption;
        this.autoCloseMenu = this.user.preferences.autoCloseMenu;
        this.readerMode = this.user.preferences.readerMode;


        this.generalSettingsForm = this.formBuilder.group({
          autoCloseMenu: this.autoCloseMenu,
          pageSplitOption: this.pageSplitOption + '',
          fittingOption: this.translateScalingOption(this.scalingOption)
        });

        this.updateForm();

        this.generalSettingsForm.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((changes: SimpleChanges) => {
          this.autoCloseMenu = this.generalSettingsForm.get('autoCloseMenu')?.value;
          // On change of splitting, re-render the page if the page is already split
          const needsSplitting = this.canvasImage.width > this.canvasImage.height;
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
      } else {
        // If no user, we can't render 
        this.router.navigateByUrl('/home');
      }
    });


    this.init();
  }

  ngAfterViewInit() {
    if (!this.canvas) {
      return;
    }
    this.ctx = this.canvas.nativeElement.getContext('2d', { alpha: false });
    this.canvasImage.onload = () => this.renderPage();
  }

  ngOnDestroy() {
    this.readerService.resetOverrideStyles();
    this.navService.showNavBar();
    this.onDestroy.next();
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW || event.key === KEY_CODES.DOWN_ARROW) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.nextPage() : this.prevPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW || event.key === KEY_CODES.UP_ARROW) {
      this.readingDirection === ReadingDirection.LeftToRight ? this.prevPage() : this.nextPage();
    } else if (event.key === KEY_CODES.ESC_KEY) {
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
    }
  }

  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;
    this.pageNum = 1;

    forkJoin({
      bookmark: this.readerService.getBookmark(this.chapterId),
      chapterInfo: this.readerService.getChapterInfo(this.chapterId)
    }).pipe(take(1)).subscribe(results => {
      this.volumeId = results.chapterInfo.volumeId;
      this.maxPages = results.chapterInfo.pages;
      this.pageOptions.ceil = this.maxPages;

      let page = results.bookmark.pageNum;
      if (page >= this.maxPages) {
        page = this.maxPages - 1;
      }
      this.setPageNum(page);

      // Due to change detection rules in Angular, we need to re-create the options object to apply the change
      const newOptions: Options = Object.assign({}, this.pageOptions);
      newOptions.ceil = this.maxPages;
      this.pageOptions = newOptions;

      this.updateTitle(results.chapterInfo);

      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST) {
          this.nextChapterDisabled = true;
        }
      });
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        if (chapterId === CHAPTER_ID_DOESNT_EXIST) {
          this.prevChapterDisabled = true;
        }
      });

      // ! Should I move the prefetching code if we start in webtoon reader mode? 
      const images = [];
      for (let i = 0; i < PREFETCH_PAGES + 2; i++) {
        images.push(new Image());
      }

      this.cachedImages = new CircularArray<HTMLImageElement>(images, 0);


      this.render();
    }, () => {
      setTimeout(() => {
        this.closeReader();
      }, 200);
    });
  }

  render() {
    if (this.readerMode === READER_MODE.WEBTOON) {
      this.isLoading = false;
    } else {
      this.loadPage();
    }
  }

  closeReader() {
    this.location.back();
  }

  updateTitle(chapterInfo: ChapterInfo) {
      this.title = chapterInfo.seriesName;
      if (chapterInfo.chapterTitle.length > 0) {
        this.title += ' - ' + chapterInfo.chapterTitle;
      }

      this.subtitle = '';
      if (chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
        this.subtitle = chapterInfo.fileName;
      } else if (!chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
        this.subtitle = 'Chapter ' + chapterInfo.chapterNumber;
      } else {
        this.subtitle = 'Volume ' + chapterInfo.volumeNumber;

        if (chapterInfo.chapterNumber !== '0') {
          this.subtitle += ' Chapter ' + chapterInfo.chapterNumber;
        }
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
    if (formControl === undefined) {
      return FITTING_OPTION.HEIGHT;
    }
    return formControl?.value;
  }

  getFittingIcon() {
    let value = FITTING_OPTION.HEIGHT;
    const formControl = this.generalSettingsForm.get('fittingOption');
    if (formControl !== undefined) {
      value = formControl?.value;
    }
    
    switch(value) {
      case FITTING_OPTION.HEIGHT:
        return 'fa-arrows-alt-v';
      case FITTING_OPTION.WIDTH:
        return 'fa-arrows-alt-h';
      case FITTING_OPTION.ORIGINAL:
        return 'fa-expand-arrows-alt';
    }
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
    
    if (this.menuTimeout) {
      clearTimeout(this.menuTimeout);
    }

    if (this.menuOpen && !this.settingsOpen) {
      this.startMenuCloseTimer();
    } else {
      this.showClickOverlay = false;
      this.settingsOpen = false;
    }
  }

  isSplitLeftToRight() {
    return (this.generalSettingsForm?.get('pageSplitOption')?.value + '') === (PageSplitOption.SplitLeftToRight + '');
  }

  isNoSplit() {
    return (this.generalSettingsForm?.get('pageSplitOption')?.value + '') === (PageSplitOption.NoSplit + '');
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
    if (this.readerMode === READER_MODE.WEBTOON) {
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

    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART);

    if ((this.pageNum + 1 >= this.maxPages && notInSplit) || this.isLoading) {

      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.FORWARD;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum + 1);
      if (this.readerMode !== READER_MODE.WEBTOON) {
        this.canvasImage = this.cachedImages.next();
      }
    }

    if (this.readerMode !== READER_MODE.WEBTOON) {
      this.loadPage();
    }    
  }

  prevPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const notInSplit = this.currentImageSplitPart !== (this.isSplitLeftToRight() ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART);

    if ((this.pageNum - 1 < 0 && notInSplit) || this.isLoading) {

      if (this.isLoading) { return; }

      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    if (this.isNoSplit() || notInSplit) {
      this.setPageNum(this.pageNum - 1);
      this.canvasImage = this.cachedImages.prev();
    }

    if (this.readerMode !== READER_MODE.WEBTOON) {
      this.loadPage();
    }  
  }

  loadNextChapter() {
    if (this.nextPageDisabled) { return; }
    this.isLoading = true;
    if (this.nextChapterId === CHAPTER_ID_NOT_FETCHED) {
      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        this.loadChapter(chapterId, 'next');
      });
    } else {
      this.loadChapter(this.nextChapterId, 'next');
    }
  }

  loadPrevChapter() {
    if (this.prevPageDisabled) { return; }
    this.isLoading = true;
    this.continuousChaptersStack.pop();
    const prevChapter = this.continuousChaptersStack.peek();
    if (prevChapter != this.chapterId) {
      if (prevChapter !== undefined) {
        this.chapterId = prevChapter;
        this.init();
        return;
      }
    }

    if (this.prevChapterId === CHAPTER_ID_NOT_FETCHED) {
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        this.loadChapter(chapterId, 'prev');
      });
    } else {
      this.loadChapter(this.prevChapterId, 'prev');
    }
  }

  loadChapter(chapterId: number, direction: 'next' | 'prev') {
    if (chapterId >= 0) {
      this.chapterId = chapterId;
      this.continuousChaptersStack.push(chapterId); 
      // Load chapter Id onto route but don't reload
      const lastSlashIndex = this.router.url.lastIndexOf('/');
      const newRoute = this.router.url.substring(0, lastSlashIndex + 1) + this.chapterId + '';
      window.history.replaceState({}, '', newRoute);
      this.init();
    } else {
      // This will only happen if no actual chapter can be found
      this.toastr.warning('Could not find ' + direction + ' chapter');
      this.isLoading = false;
      if (direction === 'prev') {
        this.prevPageDisabled = true;
      } else {
        this.nextPageDisabled = true;
      }
      
    }
  }

  renderPage() {
    if (this.ctx && this.canvas) {
      this.canvasImage.onload = null;
      //this.ctx.imageSmoothingEnabled = true;
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


  prefetch() {
    let index = 1;

    this.cachedImages.applyFor((item, i) => {
      const offsetIndex = this.pageNum + index;
      const urlPageNum = this.readerService.imageUrlToPageNum(item.src);
      if (urlPageNum === offsetIndex) {
        index += 1;
        return;
      }
      if (offsetIndex < this.maxPages - 1) {
        item.src = this.readerService.getPageUrl(this.chapterId, offsetIndex);
        index += 1;
      }
    }, this.cachedImages.size() - 3);
  }

  loadPage() {
    if (!this.canvas || !this.ctx) { return; }

    // Due to the fact that we start at image 0, but page 1, we need the last page to be bookmarked as page + 1 to be completed
    let pageNum = this.pageNum;
    if (this.pageNum == this.maxPages - 1) {
      pageNum = this.pageNum + 1;
    }


    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, pageNum).pipe(take(1)).subscribe(() => {/* No operation */});

    this.isLoading = true;
    this.canvasImage = this.cachedImages.current();
    if (this.readerService.imageUrlToPageNum(this.canvasImage.src) !== this.pageNum || this.canvasImage.src === '' || !this.canvasImage.complete) {
      this.canvasImage.src = this.readerService.getPageUrl(this.chapterId, this.pageNum);
      this.canvasImage.onload = () => this.renderPage();
    } else {
      this.renderPage();
    }
    this.prefetch();
  }

  setReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }

    if (this.menuOpen) {
      this.showClickOverlay = true;
      setTimeout(() => {
        this.showClickOverlay = false;
      }, CLICK_OVERLAY_TIMEOUT);
    }
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

  sliderPageUpdate(context: ChangeContext) {
    const page = context.value;
    
    if (page > this.pageNum) {
      this.pagingDirection = PAGING_DIRECTION.FORWARD;
    } else {
      this.pagingDirection = PAGING_DIRECTION.BACKWARDS;
    }

    this.setPageNum(page);
    this.render();
  }

  setPageNum(pageNum: number) {
    this.pageNum = pageNum;

    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (this.nextChapterId > 0 && !this.nextChapterPrefetched) {
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1)).subscribe(res => {
          this.nextChapterPrefetched = true;
        });
      }
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
    this.render();
  }

  promptForPage() {
    const goToPageNum = window.prompt('There are ' + this.maxPages + ' pages. What page would you like to go to?', '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  toggleColorMode() {
    switch(this.colorMode) {
      case COLOR_FILTER.NONE:
        this.colorMode = COLOR_FILTER.DARK;
        break;
      case COLOR_FILTER.DARK:
        this.colorMode = COLOR_FILTER.SEPIA;
        break;
      case COLOR_FILTER.SEPIA:
        this.colorMode = COLOR_FILTER.NONE;
        break;
    }
  }

  toggleReaderMode() {
    switch(this.readerMode) {
      case READER_MODE.MANGA_LR:
        this.readerMode = READER_MODE.MANGA_UD;
        break;
      case READER_MODE.MANGA_UD:
        //this.readerMode = READER_MODE.WEBTOON;
        // Temp disable ability to use webtoon
        this.readerMode = READER_MODE.MANGA_LR;
        break;
      case READER_MODE.WEBTOON:
        this.readerMode = READER_MODE.MANGA_LR;
        break;
    }

    this.updateForm();

    this.render();
  }

  updateForm() {
    if ( this.readerMode === READER_MODE.WEBTOON) {
      this.generalSettingsForm.get('fittingOption')?.disable()
      this.generalSettingsForm.get('pageSplitOption')?.disable();
    } else {
      this.generalSettingsForm.get('fittingOption')?.enable()
      this.generalSettingsForm.get('pageSplitOption')?.enable();
    }
  }

  handleWebtoonPageChange(updatedPageNum: number) {
    console.log('[MangaReader] Handling Page Change');

    this.pageNum = updatedPageNum;

    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});
    if (this.pageNum >= this.maxPages - 10) {
      // Tell server to cache the next chapter
      if (this.nextChapterId > 0 && !this.nextChapterPrefetched) {
        this.readerService.getChapterInfo(this.nextChapterId).pipe(take(1)).subscribe(res => {
          this.nextChapterPrefetched = true;
        });
      }
    }
  }

  saveSettings() {
    if (this.user === undefined) return;

    const data: Preferences = {
      readingDirection: this.readingDirection, 
      scalingOption: this.scalingOption, 
      pageSplitOption: this.pageSplitOption,
      autoCloseMenu: this.autoCloseMenu,
      readerMode: this.readerMode,

      bookReaderDarkMode: this.user.preferences.bookReaderDarkMode,
      bookReaderFontFamily: this.user.preferences.bookReaderFontFamily,
      bookReaderFontSize: this.user.preferences.bookReaderFontSize,
      bookReaderLineSpacing: this.user.preferences.bookReaderLineSpacing,
      bookReaderMargin: this.user.preferences.bookReaderMargin,
      bookReaderTapToPaginate: this.user.preferences.bookReaderTapToPaginate,
      bookReaderReadingDirection: this.readingDirection,

      siteDarkMode: this.user.preferences.siteDarkMode,
    };
    this.accountService.updatePreferences(data).pipe(take(1)).subscribe((updatedPrefs) => {
      this.toastr.success('User settings updated');
      if (this.user) {
        this.user.preferences = updatedPrefs;
      }
      this.resetSettings();
    });

  }

  resetSettings() {
    this.generalSettingsForm.get('fittingOption')?.value.get('fittingOption')?.setValue(this.translateScalingOption(this.user.preferences.scalingOption));
    this.generalSettingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption + '');
    this.generalSettingsForm.get('autoCloseMenu')?.setValue(this.autoCloseMenu);

    this.updateForm();
  }
}
