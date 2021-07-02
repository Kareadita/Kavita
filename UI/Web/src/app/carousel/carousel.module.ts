import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CarouselReelComponent } from './carousel-reel/carousel-reel.component';
import { SwiperModule } from 'swiper/angular';



@NgModule({
  declarations: [CarouselReelComponent],
  imports: [
    CommonModule,
    SwiperModule
  ],
  exports: [
    CarouselReelComponent
  ]
})
export class CarouselModule { }
