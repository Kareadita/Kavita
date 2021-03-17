import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CarouselReelComponent } from './carousel-reel/carousel-reel.component';
import { VirtualScrollerModule } from 'ngx-virtual-scroller';



@NgModule({
  declarations: [CarouselReelComponent],
  imports: [
    CommonModule,
    VirtualScrollerModule
  ],
  exports: [
    CarouselReelComponent
  ]
})
export class CarouselModule { }
