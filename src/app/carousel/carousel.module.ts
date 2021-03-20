import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CarouselReelComponent } from './carousel-reel/carousel-reel.component';



@NgModule({
  declarations: [CarouselReelComponent],
  imports: [
    CommonModule,
  ],
  exports: [
    CarouselReelComponent
  ]
})
export class CarouselModule { }
