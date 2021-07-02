import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, Renderer2, RendererStyleFlags2, ViewChild } from '@angular/core';
import {Location} from '@angular/common';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, fromEvent, Subject } from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import { BookService } from '../book.service';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';
import { BookChapterItem } from '../_models/book-chapter-item';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Stack } from 'src/app/shared/data-structures/stack';
import { Preferences } from 'src/app/_models/preferences/preferences';
import { MemberService } from 'src/app/_services/member.service';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';


interface PageStyle {
  'font-family': string;
  'font-size': string; 
  'line-height': string;
  'margin-left': string;
  'margin-right': string;
}

interface HistoryPoint {
  page: number;
  scrollOffset: number;
}

const TOP_OFFSET = -50 * 1.5; // px the sticky header takes up
const SCROLL_PART_TIMEOUT = 5000;

@Component({
  selector: 'app-book-reader',
  templateUrl: './book-reader.component.html',
  styleUrls: ['./book-reader.component.scss'],
  animations: [
    trigger('isLoading', [
      state('false', style({opacity: 1})),
      state('true', style({opacity: 0})),
      transition('false <=> true', animate('200ms'))
    ]),
    trigger('fade', [
      state('true', style({opacity: 0})),
      state('false', style({opacity: 0.4})),
      transition('false <=> true', animate('4000ms'))
    ])
  ]
})
export class BookReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;

  chapters: Array<BookChapterItem> = [];

  pageNum = 0;
  maxPages = 1;
  adhocPageHistory: Stack<HistoryPoint> = new Stack<HistoryPoint>();
  
  user!: User;

  drawerOpen = false;
  isLoading = true; 
  bookTitle: string = '';
  settingsForm: FormGroup = new FormGroup({});
  clickToPaginate = false;
  clickToPaginateVisualOverlay = false;
  clickToPaginateVisualOverlayTimeout: any = undefined; // For animation
  clickToPaginateVisualOverlayTimeout2: any = undefined; // For kicking off animation, giving enough time to render html

  page: SafeHtml | undefined = undefined; // This is the html we get from the server
  styles: SafeHtml | undefined = undefined; // This is the css we get from the server

  @ViewChild('readingHtml', {static: false}) readingHtml!: ElementRef<HTMLDivElement>;
  @ViewChild('readingSection', {static: false}) readingSectionElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('stickyTop', {static: false}) stickyTopElemRef!: ElementRef<HTMLDivElement>;


  /**
   * Internal property used to capture all the different css properties to render on all elements
   */
  pageStyles!: PageStyle;
  /**
   * List of all font families user can select from
   */
  fontFamilies: Array<string> = [];

  
  darkMode = false;
  backgroundColor: string = 'white';
  readerStyles: string = '';
  darkModeStyleElem!: HTMLElement;
  topOffset: number = 0; // Offset for drawer and rendering canvas
  scrollbarNeeded = false; // Used for showing/hiding bottom action bar
  readingDirection: ReadingDirection = ReadingDirection.LeftToRight;

  private readonly onDestroy = new Subject<void>();

  pageAnchors: {[n: string]: number } = {};
  currentPageAnchor: string = '';
  intersectionObserver: IntersectionObserver = new IntersectionObserver((entries) => this.handleIntersection(entries), { threshold: [1] });
  /**
   * Last seen bookmark part path
   */
  lastSeenScrollPartPath: string = '';

  // Temp hack: Override background color for reader and restore it onDestroy
  originalBodyColor: string | undefined;



  darkModeStyles = `
    *:not(input), *:not(select), *:not(code), *:not(:link), *:not(.ngx-toastr) {
        color: #dcdcdc !important;
    }

    code {
        color: #e83e8c !important;
    }

    // .btn-icon {
    //     background-color: transparent;
    // }

    :link, a {
        color: #8db2e5 !important;
    }

    img, img[src] {
      z-index: 1;
      filter: brightness(0.85) !important;
      background-color: initial !important;
    }
  `;

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
    private seriesService: SeriesService, private readerService: ReaderService, private location: Location,
    private renderer: Renderer2, private navService: NavService, private toastr: ToastrService, 
    private domSanitizer: DomSanitizer, private bookService: BookService, private memberService: MemberService) {
      this.navService.hideNavBar();

      this.darkModeStyleElem = this.renderer.createElement('style');
      this.darkModeStyleElem.innerHTML = this.darkModeStyles;
      this.fontFamilies = this.bookService.getFontFamilies();

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.user = user;
          
          if (this.user.preferences.bookReaderFontFamily === undefined) {
            this.user.preferences.bookReaderFontFamily = 'default';
          }
          if (this.user.preferences.bookReaderFontSize === undefined) {
            this.user.preferences.bookReaderFontSize = 100;
          }
          if (this.user.preferences.bookReaderLineSpacing === undefined) {
            this.user.preferences.bookReaderLineSpacing = 100;
          }
          if (this.user.preferences.bookReaderMargin === undefined) {
            this.user.preferences.bookReaderMargin = 0;
          }
          if (this.user.preferences.bookReaderReadingDirection === undefined) {
            this.user.preferences.bookReaderReadingDirection = ReadingDirection.LeftToRight;
          }

          this.readingDirection = this.user.preferences.bookReaderReadingDirection;

          this.clickToPaginate = this.user.preferences.bookReaderTapToPaginate;
          
          this.settingsForm.addControl('bookReaderFontFamily', new FormControl(user.preferences.bookReaderFontFamily, []));
  
          this.settingsForm.get('bookReaderFontFamily')!.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(changes => {
            this.updateFontFamily(changes);
          });
        }

        const bodyNode = document.querySelector('body');
        if (bodyNode !== undefined && bodyNode !== null) {
          this.originalBodyColor = bodyNode.style.background;
        }
        this.resetSettings();
      });
  }

  ngAfterViewInit() {
    // check scroll offset and if offset is after any of the "id" markers, bookmark it
    fromEvent(window, 'scroll')
      .pipe(debounceTime(200), takeUntil(this.onDestroy)).subscribe((event) => {
        if (this.isLoading) return;
        if (Object.keys(this.pageAnchors).length === 0) return;
        // get the height of the document so we can capture markers that are halfway on the document viewport
        const verticalOffset = (window.pageYOffset 
          || document.documentElement.scrollTop 
          || document.body.scrollTop || 0) + (document.body.offsetHeight / 2);
      
        const alreadyReached = Object.values(this.pageAnchors).filter((i: number) => i <= verticalOffset);
        if (alreadyReached.length > 0) {
          this.currentPageAnchor = Object.keys(this.pageAnchors)[alreadyReached.length - 1];
        } else {
          this.currentPageAnchor = '';
        }

        if (this.lastSeenScrollPartPath !== '') {
          this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum, this.lastSeenScrollPartPath).pipe(take(1)).subscribe(() => {/* No operation */});
        }
    });
  }

  ngOnDestroy(): void {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null && this.originalBodyColor !== undefined) {
      bodyNode.style.background = this.originalBodyColor;
      if (this.user.preferences.siteDarkMode) {
        bodyNode.classList.add('bg-dark');
      }
    }
    this.navService.showNavBar();

    const head = document.querySelector('head');
    this.renderer.removeChild(head, this.darkModeStyleElem);

    if (this.clickToPaginateVisualOverlayTimeout !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout);
      this.clickToPaginateVisualOverlayTimeout = undefined;
    }
    if (this.clickToPaginateVisualOverlayTimeout2 !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout2);
      this.clickToPaginateVisualOverlayTimeout2 = undefined;
    }

    this.onDestroy.next();
    this.onDestroy.complete();
    this.intersectionObserver.disconnect();
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

    this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(hasProgress => {
      if (!hasProgress) {
        this.toggleDrawer();
        this.toastr.info('You can modify book settings, save those settings for all books, and view table of contents from the drawer.');
      }
    });

    forkJoin({
      chapter: this.seriesService.getChapter(this.chapterId),
      bookmark: this.readerService.getBookmark(this.chapterId),
      chapters: this.bookService.getBookChapters(this.chapterId),
      info: this.bookService.getBookInfo(this.chapterId)
    }).pipe(take(1)).subscribe(results => {
      this.chapter = results.chapter;
      this.volumeId = results.chapter.volumeId;
      this.maxPages = results.chapter.pages;
      this.chapters = results.chapters;
      this.pageNum = results.bookmark.pageNum;
      this.bookTitle = results.info;


      if (this.pageNum >= this.maxPages) {
        this.pageNum = this.maxPages - 1;
        this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});
      }

      // Check if user bookmark has part, if so load it so we scroll to it
      this.loadPage(results.bookmark.bookScrollId || undefined);
    }, () => {
      setTimeout(() => {
        this.closeReader();
      }, 200);
    });
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.nextPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.prevPage();
    } else if (event.key === KEY_CODES.ESC_KEY) {
      this.closeReader();
    } else if (event.key === KEY_CODES.SPACE) {
      this.toggleDrawer();
      event.stopPropagation();
      event.preventDefault(); 
    } else if (event.key === KEY_CODES.G) {
      this.goToPage();
    }
  }

  handleIntersection(entries: IntersectionObserverEntry[]) {
    const intersectingEntries = Array.from(entries).filter(entry => entry.isIntersecting).map(entry => entry.target);
    intersectingEntries.sort((a: Element, b: Element) => {
      const aTop = a.getBoundingClientRect().top;
      const bTop = b.getBoundingClientRect().top;
      if (aTop < bTop) {
        return -1;
      }
      if (aTop > bTop) {
        return 1;
      }

      return 0;
    });

    if (intersectingEntries.length > 0) {
      let path = this.getXPathTo(intersectingEntries[0]);
        if (path === '') { return; }
        if (!path.startsWith('id')) {
          path = '//html[1]/' + path;
        }
        this.lastSeenScrollPartPath = path;
    }
  }

  loadChapter(pageNum: number, part: string) {
    this.setPageNum(pageNum);
    this.loadPage('id("' + part + '")');
  }

  closeReader() {
    this.location.back();
  }

  resetSettings() {
    const windowWidth = window.innerWidth
      || document.documentElement.clientWidth
      || document.body.clientWidth;

    let margin = '15%';
    if (windowWidth <= 700) {
      margin = '0%';
    }
    if (this.user) {
      if (windowWidth > 700) {
        margin = this.user.preferences.bookReaderMargin + '%';
      }
      this.pageStyles = {'font-family': this.user.preferences.bookReaderFontFamily, 'font-size': this.user.preferences.bookReaderFontSize + '%', 'margin-left': margin, 'margin-right': margin, 'line-height': this.user.preferences.bookReaderLineSpacing + '%'};
      if (this.user.preferences.siteDarkMode && !this.user.preferences.bookReaderDarkMode) {
        this.user.preferences.bookReaderDarkMode = true;
      }
      this.toggleDarkMode(this.user.preferences.bookReaderDarkMode);
    } else {
      this.pageStyles = {'font-family': 'default', 'font-size': '100%', 'margin-left': margin, 'margin-right': margin, 'line-height': '100%'};
      this.toggleDarkMode(false);
    }
    
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.updateReaderStyles();
  }

  /**
   * Adds a click handler for any anchors that have 'kavita-page'. If 'kavita-page' present, changes page to kavita-page and optionally passes a part value 
   * from 'kavita-part', which will cause the reader to scroll to the marker. 
   */
  addLinkClickHandlers() {
    var links = this.readingSectionElemRef.nativeElement.querySelectorAll('a');
      links.forEach((link: any) => {
        link.addEventListener('click', (e: any) => {
          if (!e.target.attributes.hasOwnProperty('kavita-page')) { return; }
          var page = parseInt(e.target.attributes['kavita-page'].value, 10);
          if (this.adhocPageHistory.peek()?.page !== this.pageNum) {
            this.adhocPageHistory.push({page: this.pageNum, scrollOffset: window.pageYOffset});
          }
          
          var partValue = e.target.attributes.hasOwnProperty('kavita-part') ? e.target.attributes['kavita-part'].value : undefined;
          if (partValue && page === this.pageNum) {
            this.scrollTo(e.target.attributes['kavita-part'].value);
            return;
          }
          
          this.setPageNum(page);
          this.loadPage(partValue);
        });
      });
  }

  moveFocus() {
    const elems = document.getElementsByClassName('reading-section');
    if (elems.length > 0) {
      (elems[0] as HTMLDivElement).focus();
    }
  }

  promptForPage() {
    const question = 'There are ' + (this.maxPages - 1) + ' pages. What page do you want to go to?';
    const goToPageNum = window.prompt(question, '');
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

    if (page === undefined || this.pageNum === page) { return; }

    if (page > this.maxPages) {
      page = this.maxPages;
    } else if (page < 0) {
      page = 0;
    }

    if (!(page === 0 || page === this.maxPages - 1)) {
      page -= 1;
    }

    this.pageNum = page;
    this.loadPage();

  }

  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  getPageMarkers(ids: Array<string>) {
    try {
      return document.querySelectorAll(ids.map(id => '#' + this.cleanIdSelector(id)).join(', '));
    } catch (Exception) {
      // Fallback to anchors instead. Some books have ids that are not valid for querySelectors, so anchors should be used instead
      return document.querySelectorAll(ids.map(id => '[href="#' + id + '"]').join(', '));
    }
  }

  setupPageAnchors() {
    this.readingSectionElemRef.nativeElement.querySelectorAll('div,o,p,ul,li,a,img,h1,h2,h3,h4,h5,h6,span').forEach(elem => {
      this.intersectionObserver.observe(elem);
    });

    this.pageAnchors = {};
    this.currentPageAnchor = '';
    const ids = this.chapters.map(item => item.children).flat().filter(item => item.page === this.pageNum).map(item => item.part).filter(item => item.length > 0);
    if (ids.length > 0) {
      const elems = this.getPageMarkers(ids);
      elems.forEach(elem => {
        this.pageAnchors[elem.id] = elem.getBoundingClientRect().top;
      });
    }
  }

  loadPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = true;

    this.readerService.bookmark(this.seriesId, this.volumeId, this.chapterId, this.pageNum).pipe(take(1)).subscribe(() => {/* No operation */});

    this.bookService.getBookPage(this.chapterId, this.pageNum).pipe(take(1)).subscribe(content => {
      this.page = this.domSanitizer.bypassSecurityTrustHtml(content);
      setTimeout(() => {
        this.addLinkClickHandlers();
        this.updateReaderStyles();
        this.topOffset = this.stickyTopElemRef.nativeElement?.offsetHeight;

        Promise.all(Array.from(this.readingSectionElemRef.nativeElement.querySelectorAll('img'))
        .filter(img => !img.complete)
        .map(img => new Promise(resolve => { img.onload = img.onerror = resolve; })))
        .then(() => {
          this.isLoading = false;
          this.scrollbarNeeded = this.readingSectionElemRef.nativeElement.scrollHeight > this.readingSectionElemRef.nativeElement.clientHeight;

          // Find all the part ids and their top offset
          this.setupPageAnchors();
          

          if (part !== undefined && part !== '') {
            this.scrollTo(part);
          } else if (scrollTop !== undefined && scrollTop !== 0) {
            window.scroll({
              top: scrollTop,
              behavior: 'smooth'
            });
          } else {
            window.scroll({
              top: 0,
              behavior: 'smooth'
            });
          }
        });
      }, 10);
    });
  }

  setPageNum(pageNum: number) {
    if (pageNum < 0) {
      this.pageNum = 0;
    } else if (pageNum >= this.maxPages - 1) {
      this.pageNum = this.maxPages - 1;
    } else {
      this.pageNum = pageNum;
    }
  }

  goBack() {
    if (!this.adhocPageHistory.isEmpty()) {
      const page = this.adhocPageHistory.pop();
      if (page !== undefined) {
        this.setPageNum(page.page);
        this.loadPage(undefined, page.scrollOffset);
      }
    }
  }

  prevPage() {
    const oldPageNum = this.pageNum;
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.setPageNum(this.pageNum - 1);
    } else {
      this.setPageNum(this.pageNum + 1);
    }

    if (oldPageNum === this.pageNum) { return; }
    this.loadPage();
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const oldPageNum = this.pageNum;
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.setPageNum(this.pageNum + 1);
    } else {
      this.setPageNum(this.pageNum - 1);
    }
    
    if (oldPageNum === this.pageNum) { return; }

    this.loadPage();
  }

  updateFontSize(amount: number) {
    let val = parseInt(this.pageStyles['font-size'].substr(0, this.pageStyles['font-size'].length - 1), 10);
    
    if (val + amount > 300 || val + amount < 50) {
      return;
    }

    this.pageStyles['font-size'] = val + amount + '%';
    this.updateReaderStyles();
  }

  updateFontFamily(familyName: string) {
    if (familyName === null) familyName = '';
    let cleanedName = familyName.replace(' ', '_').replace('!important', '').trim();
    if (cleanedName === 'default') {
      this.pageStyles['font-family'] = 'inherit';
    } else {
      this.pageStyles['font-family'] = "'" + cleanedName + "'";
    }

    this.updateReaderStyles();
  }

  updateMargin(amount: number) {
    let cleanedValue = this.pageStyles['margin-left'].replace('%', '').replace('!important', '').trim();
    let val = parseInt(cleanedValue, 10);

    if (val + amount > 30 || val + amount < 0) {
      return;
    }

    this.pageStyles['margin-left'] = (val + amount) + '%';
    this.pageStyles['margin-right'] = (val + amount) + '%';

    this.updateReaderStyles();
  }

  updateLineSpacing(amount: number) {
    const cleanedValue = parseInt(this.pageStyles['line-height'].replace('%', '').replace('!important', '').trim(), 10);

    if (cleanedValue + amount > 250 || cleanedValue + amount < 100) {
      return;
    }

    this.pageStyles['line-height'] = (cleanedValue + amount) + '%';

    this.updateReaderStyles();
  }

  updateReaderStyles() {
    if (this.readingHtml != undefined && this.readingHtml.nativeElement) {
      for(let i = 0; i < this.readingHtml.nativeElement.children.length; i++) {
        const elem = this.readingHtml.nativeElement.children.item(i);
        if (elem?.tagName != 'STYLE') {
          Object.entries(this.pageStyles).forEach(item => {
            this.renderer.setStyle(elem, item[0], item[1], RendererStyleFlags2.Important);
          });
        }
      }
    }
  }


  toggleDarkMode(force?: boolean) {
    if (force !== undefined) {
      this.darkMode = force;
    } else {
      this.darkMode = !this.darkMode;
    }

    this.setOverrideStyles();
  }

  toggleReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }
  }

  getDarkModeBackgroundColor() {
    return this.darkMode ? '#292929' : '#fff';
  }

  setOverrideStyles() {
    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null) {
      if (this.user.preferences.siteDarkMode) {
        bodyNode.classList.remove('bg-dark');
      }
      
      bodyNode.style.background = this.getDarkModeBackgroundColor();
    }
    this.backgroundColor = this.getDarkModeBackgroundColor();
    const head = document.querySelector('head');
    if (this.darkMode) {
      this.renderer.appendChild(head, this.darkModeStyleElem)
    } else {
      this.renderer.removeChild(head, this.darkModeStyleElem);
    }
  }

  saveSettings() {
    if (this.user === undefined) return;
    const modelSettings = this.settingsForm.value;
    const data: Preferences = {
      readingDirection: this.user.preferences.readingDirection, 
      scalingOption: this.user.preferences.scalingOption, 
      pageSplitOption: this.user.preferences.pageSplitOption, 
      autoCloseMenu: this.user.preferences.autoCloseMenu,
      readerMode: this.user.preferences.readerMode,
      bookReaderDarkMode: this.darkMode,
      bookReaderFontFamily: modelSettings.bookReaderFontFamily,
      bookReaderFontSize: parseInt(this.pageStyles['font-size'].substr(0, this.pageStyles['font-size'].length - 1), 10),
      bookReaderLineSpacing: parseInt(this.pageStyles['line-height'].replace('!important', '').trim(), 10),
      bookReaderMargin: parseInt(this.pageStyles['margin-left'].replace('%', '').replace('!important', '').trim(), 10),
      bookReaderTapToPaginate: this.clickToPaginate,
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

  toggleDrawer() {
    this.topOffset = this.stickyTopElemRef.nativeElement?.offsetHeight;
    this.drawerOpen = !this.drawerOpen;
  }

  closeDrawer() {
    this.drawerOpen = false;
  }

  handleReaderClick(event: MouseEvent) {
    if (this.drawerOpen) {
      this.closeDrawer();
      event.stopPropagation();
      event.preventDefault();
    }
  }


  scrollTo(partSelector: string) {
    if (partSelector.startsWith('#')) {
      partSelector = partSelector.substr(1, partSelector.length);
    }

    let element = null;
    if (partSelector.startsWith('//') || partSelector.startsWith('id(')) {
      // Part selector is a XPATH
      element = this.getElementFromXPath(partSelector);
    } else {
      element = document.querySelector('*[id="' + partSelector + '"]');
    }

    if (element === null) return;

    window.scroll({
      top: element.getBoundingClientRect().top + window.pageYOffset + TOP_OFFSET,
      behavior: 'smooth' 
    });
  }

  toggleClickToPaginate() {
    this.clickToPaginate = !this.clickToPaginate;

    if (this.clickToPaginateVisualOverlayTimeout2 !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout2);
      this.clickToPaginateVisualOverlayTimeout2 = undefined;
    }
    if (!this.clickToPaginate) { return; }

    this.clickToPaginateVisualOverlayTimeout2 = setTimeout(() => {
      this.showClickToPaginateVisualOverlay();
    }, 200);
  }

  showClickToPaginateVisualOverlay() {
    this.clickToPaginateVisualOverlay = true;

    if (this.clickToPaginateVisualOverlay && this.clickToPaginateVisualOverlayTimeout !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout);
      this.clickToPaginateVisualOverlayTimeout = undefined;
    }
    this.clickToPaginateVisualOverlayTimeout = setTimeout(() => {
      this.clickToPaginateVisualOverlay = false;
    }, 1000);

  }

  getElementFromXPath(path: string) {
    const node = document.evaluate(path, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
    if (node?.nodeType === Node.ELEMENT_NODE) {
      return node as Element;
    }
    return null;
  }

  getXPathTo(element: any): string {
    if (element === null) return '';
    if (element.id !== '') { return 'id("' + element.id + '")'; }
    if (element === document.body) { return element.tagName; }
          
  
    let ix = 0;
    const siblings = element.parentNode?.childNodes || [];
    for (let sibling of siblings) {
        if (sibling === element) {
          return this.getXPathTo(element.parentNode) + '/' + element.tagName + '[' + (ix + 1) + ']';
        }
        if (sibling.nodeType === 1 && sibling.tagName === element.tagName) {
          ix++;
        }
            
    }
    return '';
  }

}
