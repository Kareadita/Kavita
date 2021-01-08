import { AfterViewInit, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { MangaImage } from '../_models/manga-image';
import { MemberService } from '../_services/member.service';
import { ReaderService } from '../_services/reader.service';

@Component({
  selector: 'app-manga-reader',
  templateUrl: './manga-reader.component.html',
  styleUrls: ['./manga-reader.component.scss']
})
export class MangaReaderComponent implements OnInit, AfterViewInit {

  // Is there a way to turn off the nav bar? Perhaps a service?
  isLoading = true;
  libraryId = 0;
  seriesId = 0;
  volumeId = 0;

  width = 600;
  height = 1000;
  pageNum = 0;

  imageUrl: SafeUrl | undefined = undefined;

  @ViewChild('content') canvas: ElementRef | undefined;
  private ctx: CanvasRenderingContext2D | undefined;


  constructor(private route: ActivatedRoute, private router: Router,
              private memberService: MemberService, private readerService: ReaderService,
              private sanitizer: DomSanitizer) { }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const volumeId = this.route.snapshot.paramMap.get('volumeId');

    if (libraryId === null || seriesId === null || volumeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.volumeId = parseInt(volumeId, 10);

    this.readerService.getMangaInfo(this.volumeId).subscribe(numOfPages => {
      console.log('Number of pages: ', numOfPages);
      this.loadPage();
    });

  }

  ngAfterViewInit() {
    if (!this.canvas) {
      return;
    }
    this.ctx = this.canvas.nativeElement.getContext('2d');
  }

  renderPage(image: MangaImage, imageUrl: any) {
    if (!this.canvas || !this.ctx) {
      return;
    }
    console.log('Rendering page: ', this.pageNum);


    this.ctx.clearRect (0, 0, this.width, this.height);
    this.ctx.fillStyle = '#000';
    this.ctx.canvas.width  = window.innerWidth;
    this.ctx.canvas.height = window.innerHeight;
    //this.ctx.fillRect(0, 0, this.canvas.nativeElement.width, this.canvas.nativeElement.height);
    this.width = image.width;
    this.height = image.height;

    console.log('image', image);
    const that = this;
    const img = new Image();
    img.onload = () => {
      if (that.ctx) {
        that.ctx.drawImage(img, 0, 0);
      }
    };

    img.src = 'data:image/jpeg;base64,' + image.content;
    
    this.imageUrl = this.sanitizer.bypassSecurityTrustUrl(img.src);

    //this.ctx.drawImage(img, 0, 0); // , image.width, image.height
    
  }

  nextPage() {
    this.pageNum++;
    this.loadPage();
  }

  loadPage() {
    this.isLoading = true;
    this.readerService.getPage(this.volumeId, this.pageNum).subscribe(image => {
      const reader = new FileReader();
      const that = this;
      this.isLoading = false;
      this.renderPage(image, '');
    });
  }

}
