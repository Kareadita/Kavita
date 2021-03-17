import { Component, ContentChild, ElementRef, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';

const scrollAmount = 0.2; // TODO: Make this a bit more responsive to the layout size. 0.2 works great on smaller screens, but not as good on desktop

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss']
})
export class CarouselReelComponent implements OnInit {

  @ContentChild('carouselItem') carouselItemTemplate!: TemplateRef<any>;
  @Input() items: any[] = [];
  @Input() title = '';
  @Output() sectionClick = new EventEmitter<string>();
  @ViewChild('contents') reelContents!: ElementRef;

  constructor() { }

  ngOnInit(): void {}

  nextPage() {
    const maxScrollLeft = this.reelContents.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
    const scrollPercent = Number(maxScrollLeft * scrollAmount);
    this.reelContents.nativeElement.scrollLeft += scrollPercent;
    if (this.reelContents.nativeElement.scrollLeft >= maxScrollLeft) {
      this.reelContents.nativeElement.scrollLeft = maxScrollLeft;
    }
  }

  prevPage() {
    const maxScrollLeft = this.reelContents.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
    const scrollPercent = Number(maxScrollLeft * scrollAmount);
    this.reelContents.nativeElement.scrollLeft -= scrollPercent;
    if (this.reelContents.nativeElement.scrollLeft < 0) {
      this.reelContents.nativeElement.scrollLeft = 0;
    }
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  canScrollLeft() {
    if (!this.reelContents) {
      return false;
    }
    return this.reelContents.nativeElement.scrollLeft !== 0;
  }

  canScrollRight() {
    if (!this.reelContents) {
      return false;
    }
    const maxScrollLeft = this.reelContents.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
    return this.reelContents.nativeElement.scrollLeft < maxScrollLeft;
    // TODO: There is a visible lag as UI is updating after this is called, hence state doesn't instantly reflect
  }

}
