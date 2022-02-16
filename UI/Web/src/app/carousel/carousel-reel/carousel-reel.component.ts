import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import { SwiperComponent } from 'swiper/angular';
import { Swiper, SwiperEvents } from 'swiper/types';

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

  swiper: Swiper | undefined;


  trackByIdentity: (index: number, item: any) => string;

  get isEnd() {
    return this.swiper?.isEnd;
  }

  get isBeginning() {
    return this.swiper?.isBeginning;
  }

  constructor() { 
    this.trackByIdentity = (index: number, item: any) => `${this.title}_${item.id}_${item?.name}_${item?.pagesRead}_${index}`;
  }

  ngOnInit(): void {}

  nextPage() {
    if (this.isEnd) return;
    if (this.swiper) {
      this.swiper.setProgress(this.swiper.progress + 0.25, 600);
    }
  }

  prevPage() {
    if (this.isBeginning) return;
    if (this.swiper) {
      this.swiper.setProgress(this.swiper.progress - 0.25, 600);
    }
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  onSwiper(eventParams: Parameters<SwiperEvents['init']>) {
    [this.swiper] = eventParams;
  }
}
