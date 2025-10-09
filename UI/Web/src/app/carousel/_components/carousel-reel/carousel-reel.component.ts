import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  CUSTOM_ELEMENTS_SCHEMA,
  EventEmitter,
  inject,
  Input,
  Output,
  TemplateRef
} from '@angular/core';
import {Swiper} from 'swiper/types';
import {register} from 'swiper/element/bundle';
import {NgClass, NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {ActionItem} from "../../../_services/action-factory.service";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";

register();

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, NgTemplateOutlet, TranslocoDirective, CardActionablesComponent, SafeUrlPipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class CarouselReelComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @ContentChild('carouselItem') carouselItemTemplate!: TemplateRef<any>;
  @ContentChild('promptToAdd') promptToAddTemplate!: TemplateRef<any>;
  @Input() items: any[] = [];
  @Input() title = '';
  /**
   * If provided, will render the title as an anchor
   */
  @Input() titleLink = '';
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
  /**
   * Actionables to render to the left of the title
   */
  @Input() actionables: Array<ActionItem<any>> = [];
  @Output() sectionClick = new EventEmitter<string>();
  @Output() handleAction = new EventEmitter<ActionItem<any>>();

  swiper: Swiper | undefined;

  get progressChange() {
    const totalItems = this.items.length;
    const itemsToMove = Math.min(5, totalItems);
    const progressPerItem = 1 / totalItems;
    return Math.min(0.25, progressPerItem * itemsToMove);
  }

  nextPage() {
    if (this.swiper) {
      if (this.swiper.isEnd) return;

      this.swiper.setProgress(this.swiper.progress + this.progressChange, 600);
      this.cdRef.markForCheck();
    }
  }

  prevPage() {
    if (this.swiper) {
      if (this.swiper.isBeginning) return;
      this.swiper.setProgress(this.swiper.progress - this.progressChange, 600);
      this.cdRef.markForCheck();
    }
  }

  sectionClicked(event: any) {
    this.sectionClick.emit(this.title);
  }

  // Swiper new implementation makes it so we need to use a progress event to get initialized
  onProgress(event: any) {
    let progress = 0;
    [this.swiper, progress] = event.detail;
    this.cdRef.markForCheck();
  }


  performAction(action: ActionItem<any>) {
    this.handleAction.emit(action);
  }
}
