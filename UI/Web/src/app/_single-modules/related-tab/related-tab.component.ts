import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {ReadingList} from "../../_models/reading-list";
import {CardItemComponent} from "../../cards/card-item/card-item.component";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ImageService} from "../../_services/image.service";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-related-tab',
  standalone: true,
  imports: [
    CardItemComponent,
    CarouselReelComponent,
    TranslocoDirective
  ],
  templateUrl: './related-tab.component.html',
  styleUrl: './related-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RelatedTabComponent {

  protected readonly imageService = inject(ImageService);

  @Input() readingLists: Array<ReadingList> = [];

  openReadingList(readingList: ReadingList) {

  }

}
