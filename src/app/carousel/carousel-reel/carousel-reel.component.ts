import { Component, ContentChild, ElementRef, EventEmitter, Input, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import Swiper from 'swiper';
import { PaginationOptions } from 'swiper/types';

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
  //@ViewChild('contents') public reelContents!: ElementRef;

  swiper!: Swiper;

  paginationOptions: PaginationOptions = {  type: 'custom', renderCustom: (swiper: Swiper, current: number, total: number) => {
    return current + ' of ' + total;
  }  };
  slidesPerBreakpoint = {
    640: {
      slidesPerView: 2,
      spaceBetween: 0,
    },
    768: {
      slidesPerView: 4,
      spaceBetween: 0,
    },
    1024: {
      slidesPerView: 5,
      spaceBetween: 0,
    },
      
  }

  constructor() { }

  ngOnInit(): void {}


  nextPage() {
    if (this.swiper) {
      this.swiper.setProgress(this.swiper.progress + 0.25, 600);
    }
  }

  prevPage() {
    if (this.swiper) {
      this.swiper.setProgress(this.swiper.progress - 0.25, 600);
    }
  }

  // calculateScrollAmount() {
  //   const screenWidth = window.innerWidth;
  //   const maxScrollLeft = this.reelContents.nativeElement.scrollWidth - this.reelContents.nativeElement.clientWidth;
  //   let scrollLeft = Number(maxScrollLeft * scrollAmount);
  //   if (this.reelContents.nativeElement.children.length > 0) {
  //     const itemWidth = this.reelContents.nativeElement.children[0].clientWidth;
  //     const itemsInView = (screenWidth ) / itemWidth;
  //     scrollLeft = (itemsInView - 1) * itemWidth;
  //   }
  //   return [scrollLeft, maxScrollLeft];
  // }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  get canScrollLeft() {
    if (this.swiper !== undefined) {
      return !this.swiper.isBeginning;
    }
    return false;
  }

  get canScrollRight() {
    if (this.swiper !== undefined) {
      return !this.swiper.isEnd;
    }

    return false;
  }


  onSwiper(swiper: any) {
    this.swiper = swiper;
  }
}
