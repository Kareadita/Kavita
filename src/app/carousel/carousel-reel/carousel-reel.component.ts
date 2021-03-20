import { Component, ContentChild, ElementRef, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';

const scrollAmount = 0.4;

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss']
})
export class CarouselReelComponent implements OnInit{

  @ContentChild('carouselItem') carouselItemTemplate!: TemplateRef<any>;
  @Input() items: any[] = [];
  @Input() title = '';
  @Output() sectionClick = new EventEmitter<string>();
  @ViewChild('contents') public reelContents!: ElementRef;

  constructor() { }

  ngOnInit(): void {}


  nextPage() {
    const [scrollPercent, maxWidth] = this.calculateScrollAmount();
    this.reelContents.nativeElement.scrollLeft += scrollPercent;
    if (this.reelContents.nativeElement.scrollLeft >= maxWidth) {
      this.reelContents.nativeElement.scrollLeft = maxWidth;
    }
  }

  prevPage() {
    const [scrollPercent, _] = this.calculateScrollAmount();
    this.reelContents.nativeElement.scrollLeft -= scrollPercent;
    if (this.reelContents.nativeElement.scrollLeft < 0) {
      this.reelContents.nativeElement.scrollLeft = 0;
    }
  }

  calculateScrollAmount() {
    const screenWidth = window.innerWidth;
    const maxScrollLeft = this.reelContents.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
    let scrollLeft = Number(maxScrollLeft * scrollAmount);
    if (this.reelContents.nativeElement.children.length > 0) {
      const itemWidth = this.reelContents.nativeElement.children[0].clientWidth;
      const itemsInView = (screenWidth ) / itemWidth;
      scrollLeft = (itemsInView - 1) * itemWidth;
    }
    return [scrollLeft, maxScrollLeft];
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  get canScrollLeft() {
    if (!this.reelContents?.nativeElement) {
      return false;
    }
    return this.reelContents?.nativeElement.scrollLeft !== 0;
  }

  get canScrollRight() {
    if (!this.reelContents?.nativeElement) {
      return true;
    }
    const maxScrollLeft = this.reelContents?.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
    return this.reelContents?.nativeElement.scrollLeft < maxScrollLeft;
  }

}
