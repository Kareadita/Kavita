import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import {Location} from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MangaImage } from '../_models/manga-image';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';
import { FormBuilder, FormGroup } from '@angular/forms';
import { NavService } from '../_services/nav.service';

enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft',
  ESC_KEY = 'Escape'
}

enum READING_DIRECTION {
  LEFT_TO_RIGHT = 1,
  RIGHT_TO_LEFT = 2
}

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

  width = 600;
  height = 1000;
  pageNum = 0;
  documentHeight = 0;
  maxPages = 1;
  user!: User;
  fittingForm: FormGroup | undefined;

  readingDirection = READING_DIRECTION.LEFT_TO_RIGHT; // TODO: Refactor to user settings

  images: MangaImage[] = [];
  cachedImages = new Queue<MangaImage>();
  cachedPages = new Queue<number>();

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
    const volumeId = this.route.snapshot.paramMap.get('volumeId');

    if (libraryId === null || seriesId === null || volumeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.setOverrideStyles();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
      }
    });

    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.volumeId = parseInt(volumeId, 10);

    this.fittingForm = this.formBuilder.group({
      fittingOption: FITTING_OPTION.HEIGHT
    });


    this.seriesService.getVolume(this.volumeId).subscribe(volume => {
      console.log('volume.pages', volume.pages);
      this.maxPages = volume.pages;
      this.loadPage();
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

    this.documentHeight = this.getDocumentHeight();

    console.log('Document Height: ', this.documentHeight);
  }

  ngOnDestroy() {
    console.log('onDestroy')
    for (let i = 0; i < this.maxPages; i++) {
      localStorage.removeItem(this.getPageKey(i));
    }

    const bodyNode = document.querySelector('body');
    if (bodyNode !== undefined && bodyNode !== null && this.originalBodyColor !== undefined) {
      bodyNode.style.background = this.originalBodyColor;
      bodyNode.style.height = '100%';
    }
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.readingDirection === READING_DIRECTION.LEFT_TO_RIGHT ? this.nextPage() : this.prevPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.readingDirection === READING_DIRECTION.LEFT_TO_RIGHT ? this.prevPage() : this.nextPage();
    } else if (event.key === KEY_CODES.ESC_KEY) {
      this.location.back();
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

  getFittingOptionClass() {
    if (this.fittingForm === undefined) {
      return FITTING_OPTION.HEIGHT;
    }
    return this.fittingForm.value.fittingOption;
  }

  toggleMenu() {
    this.menuOpen = !this.menuOpen;
  }

  getDocumentHeight() {
    // Do I need this?
    return Math.max(
      document.body.scrollHeight,
      document.documentElement.scrollHeight,
      document.body.offsetHeight,
      document.documentElement.offsetHeight,
      document.body.clientHeight,
      document.documentElement.clientHeight
    );
  }

  getDocumentWidth() {
    return Math.max(
      document.body.scrollWidth,
      document.documentElement.scrollWidth,
      document.body.offsetWidth,
      document.documentElement.offsetWidth,
      document.body.clientWidth,
      document.documentElement.clientWidth
    );
  }

  /**
   * @description Checks the cache interface if this page is cached.
   * @param pageNum Page number to check against
   * @return The MangaImage or undefined if it doesn't exist
   */
  checkCache(pageNum: number): MangaImage | undefined {
    const key = this.getPageKey(pageNum);
    //console.log('checking cache for key: ', key);
    const image = localStorage.getItem(key);
    if (image !== null) {
      return JSON.parse(image);
    }
    return undefined;
  }

  cache(image: MangaImage, pageNum: number) {
    this.cachedPages.enqueue(pageNum);
    this.cachedImages.enqueue(image);

    try {
      const key = this.getPageKey(pageNum);
      if (localStorage.getItem(key) === null) {
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
      }
    }

  }

  clearCanvas() {
    if (!this.canvas || !this.ctx) {
      return;
    }

    //this.ctx.clearRect(0, 0, this.getDocumentWidth(), this.getDocumentHeight()); //this.canvas.nativeElement.width, this.canvas.nativeElement.height
    this.ctx.fillStyle = '#000';
    this.ctx.fillRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height); //this.canvas.nativeElement.height
  }

  getPageKey(pageNum: number) {
    return `kavita-${this.user.username}-${this.volumeId}--${pageNum}`;
  }

  renderPage(image: MangaImage) {
    if (!this.canvas || !this.ctx) {
      return;
    }
    this.isLoading = false;
    this.clearCanvas();

    this.canvas.nativeElement.width = image.width;
    this.canvas.nativeElement.height = image.height;

    this.cache(image, this.pageNum);

    const that = this;
    const img = new Image();
    img.onload = () => {
      if (that.ctx) {
        that.ctx.drawImage(img, 0, 0);
      }
    };

    img.src = 'data:image/jpeg;base64,' + image.content;
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.pageNum + 1 >= this.maxPages) {
      return;
    }
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
      this.readerService.getPage(this.volumeId, this.pageNum).subscribe(image => {
        this.renderPage(image);
      });
    }
  }

}
