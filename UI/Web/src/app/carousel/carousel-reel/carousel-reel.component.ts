import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';
import Swiper from 'swiper';

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


  swiper!: Swiper;

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

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  onSwiper(swiper: any) {
    this.swiper = swiper;
  }
}
