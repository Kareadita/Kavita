import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  EventEmitter,
  inject,
  Input,
  Output,
  TemplateRef
} from '@angular/core';
import { Swiper, SwiperEvents } from 'swiper/types';
import { SwiperModule } from 'swiper/angular';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [NgClass, SwiperModule, NgTemplateOutlet, TranslocoDirective]
})
export class CarouselReelComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @ContentChild('carouselItem') carouselItemTemplate!: TemplateRef<any>;
  @ContentChild('promptToAdd') promptToAddTemplate!: TemplateRef<any>;
  @Input() items: any[] = [];
  @Input() title = '';
  @Input() clickableTitle: boolean = true;
  @Input() iconClasses = '';
  /**
   * Show's the carousel component even if there is nothing in it
   */
  @Input() alwaysShow = false;
  /**
   * Track by identity. By default, this has an implementation based on title, item's name, pagesRead, and index
   */
  @Input() trackByIdentity: (index: number, item: any) => string = (index: number, item: any) => `${this.title}_${item.id}_${item?.name}_${item?.pagesRead}_${index}`;
  @Output() sectionClick = new EventEmitter<string>();

  swiper: Swiper | undefined;

  nextPage() {
    if (this.swiper) {
      if (this.swiper.isEnd) return;
      this.swiper.setProgress(this.swiper.progress + 0.25, 600);
      this.cdRef.markForCheck();
    }
  }

  prevPage() {
    if (this.swiper) {
      if (this.swiper.isBeginning) return;
      this.swiper.setProgress(this.swiper.progress - 0.25, 600);
      this.cdRef.markForCheck();
    }
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  onSwiper(eventParams: Parameters<SwiperEvents['init']>) {
    [this.swiper] = eventParams;
    this.cdRef.detectChanges();
  }
}
