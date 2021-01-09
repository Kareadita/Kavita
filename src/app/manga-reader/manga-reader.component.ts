import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MangaImage } from '../_models/manga-image';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';
import { ReaderService } from '../_services/reader.service';

enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft'
}

enum READING_DIRECTION {
  LEFT_TO_RIGHT = 1,
  RIGHT_TO_LEFT = 2
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

  readingDirection = READING_DIRECTION.LEFT_TO_RIGHT; // TODO: Refactor to user settings

  images: MangaImage[] = [];
  cachedImages = new Queue<MangaImage>();
  cachedPages = new Queue<number>();

  menuOpen = false;
  isLoading = true; // we need to debounce this so it only kicks in longer than 30 ms load time

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx!: CanvasRenderingContext2D;


  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
              private memberService: MemberService, private readerService: ReaderService) { }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const volumeId = this.route.snapshot.paramMap.get('volumeId');

    if (libraryId === null || seriesId === null || volumeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
      }
    });

    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.volumeId = parseInt(volumeId, 10);

    this.readerService.getMangaInfo(this.volumeId).subscribe(numOfPages => {
      this.maxPages = numOfPages;
      this.loadPage();
    });

  }

  ngAfterViewInit() {
    if (!this.canvas) {
      return;
    }
    this.ctx = this.canvas.nativeElement.getContext('2d');

    this.documentHeight = this.getDocumentHeight();

    console.log('Document Height: ', this.documentHeight);
  }

  ngOnDestroy() {
    console.log('onDestroy - cleaning up cache');
    for (let i = 0; i < this.maxPages; i++) {
      localStorage.removeItem(this.getPageKey(i));
    }
  }

  @HostListener('window:keyup', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.readingDirection === READING_DIRECTION.LEFT_TO_RIGHT ? this.nextPage() : this.prevPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.readingDirection === READING_DIRECTION.LEFT_TO_RIGHT ? this.prevPage() : this.nextPage();
    }
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

  /**
   * @description Checks the cache interface if this page is cached.
   * @param pageNum Page number to check against
   * @return The MangaImage or undefined if it doesn't exist
   */
  checkCache(pageNum: number): MangaImage | undefined {
    const key = this.getPageKey(pageNum);
    console.log('checking cache for key: ', key);
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

    this.ctx.clearRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
    this.ctx.fillStyle = '#000';
    this.ctx.fillRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
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

  nextPage() {
    if (this.pageNum + 1 >= this.maxPages) {
      return;
    }
    this.pageNum++;
    this.loadPage();
  }

  prevPage() {
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
