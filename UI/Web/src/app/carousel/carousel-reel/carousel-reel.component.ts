import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import { SwiperComponent } from 'swiper/angular';
//import Swiper from 'swiper';
//import { SwiperEvents, Swiper } from 'swiper/types';

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

  @ViewChild('swiper', { static: false }) swiper?: SwiperComponent;


  //swiper!: Swiper;
  trackByIdentity: (index: number, item: any) => string;

  get isEnd() {
    return this.swiper?.swiperRef.isEnd;
  }

  get isBeginning() {
    return this.swiper?.swiperRef.isBeginning;
  }

  constructor() { 
    this.trackByIdentity = (index: number, item: any) => `${this.title}_${item.id}_${item?.name}_${item?.pagesRead}_${index}`;
  }

  ngOnInit(): void {}

  nextPage() {
    if (this.swiper) {
      this.swiper.swiperRef.setProgress(this.swiper.swiperRef.progress + 0.25, 600);
    }
  }

  prevPage() {
    if (this.swiper) {
      this.swiper.swiperRef.setProgress(this.swiper.swiperRef.progress - 0.25, 600);
    }
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  // onSwiper(eventParams: Parameters<SwiperEvents['init']>) {
  //   console.log('swiper: ', eventParams);
  //   [this.swiper] = eventParams;
  // }

  // onSwiper(params: Swiper) {
  //   // const [swiper] = params;
  //   // console.log(swiper);
  //   // return params;
  // }
}
