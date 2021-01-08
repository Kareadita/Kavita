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

@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss']
})
export class MangaReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  // Is there a way to turn off the nav bar? Perhaps a service?
  isLoading = true;
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

  clearCanvas() {
    if (!this.canvas || !this.ctx) {
      return;
    }

    this.ctx.clearRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
    this.ctx.fillStyle = '#000';
    this.ctx.fillRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
  }

  renderPage(image: MangaImage) {
    if (!this.canvas || !this.ctx) {
      return;
    }
    this.clearCanvas();

    this.canvas.nativeElement.width = image.width;
    this.canvas.nativeElement.height = image.height;

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
    // TODO: Check cache if we already have this page
    const key = `kavita-${this.user.username}-${this.volumeId}--${this.pageNum}`;
    console.log('checking cache for key: ', key);
    const existingImage = localStorage.getItem(key);

    this.readerService.getPage(this.volumeId, this.pageNum).subscribe(image => {
      this.isLoading = false;
      this.renderPage(image);
    });
  }

}
