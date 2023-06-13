import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CarouselReelComponent } from './_components/carousel-reel/carousel-reel.component';
import { SwiperModule } from 'swiper/angular';
import {PipeModule} from "../pipe/pipe.module";



@NgModule({
  declarations: [CarouselReelComponent],
    imports: [
        CommonModule,
        SwiperModule,
        PipeModule
    ],
  exports: [
    CarouselReelComponent
  ]
})
export class CarouselModule { }
