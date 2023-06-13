import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ContentChild, EventEmitter, Input, Output, TemplateRef } from '@angular/core';
import { Swiper, SwiperEvents } from 'swiper/types';

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CarouselReelComponent {

  @ContentChild('carouselItem') carouselItemTemplate!: TemplateRef<any>;
  @Input() items: any[] = [];
  @Input() title = '';
  @Input() clickableTitle: boolean = true;
  @Input() iconClasses = '';
  /**
   * Track by identity. By default, this has an implementation based on title, item's name, pagesRead, and index
   */
  @Input() trackByIdentity: (index: number, item: any) => string = (index: number, item: any) => `${this.title}_${item.id}_${item?.name}_${item?.pagesRead}_${index}`;
  @Output() sectionClick = new EventEmitter<string>();

  swiper: Swiper | undefined;



  constructor(private readonly cdRef: ChangeDetectorRef) {}

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
