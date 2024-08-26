import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {ReadingList} from "../../_models/reading-list";
import {CardItemComponent} from "../../cards/card-item/card-item.component";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ImageService} from "../../_services/image.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {UserCollection} from "../../_models/collection-tag";
import {Router} from "@angular/router";
import {SeriesCardComponent} from "../../cards/series-card/series-card.component";
import {Series} from "../../_models/series";
import {RelationKind} from "../../_models/series-detail/relation-kind";

export interface RelatedSeriesPair {
  series: Series;
  relation: RelationKind;
}

@Component({
  selector: 'app-related-tab',
  standalone: true,
  imports: [
    CardItemComponent,
    CarouselReelComponent,
    TranslocoDirective,
    SeriesCardComponent
  ],
  templateUrl: './related-tab.component.html',
  styleUrl: './related-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RelatedTabComponent {

  protected readonly imageService = inject(ImageService);
  protected readonly router = inject(Router);

  @Input() readingLists: Array<ReadingList> = [];
  @Input() collections: Array<UserCollection> = [];
  @Input() relations: Array<RelatedSeriesPair> = [];

  openReadingList(readingList: ReadingList) {
    this.router.navigate(['lists', readingList.id]);
  }

  openCollection(collection: UserCollection) {
    this.router.navigate(['collections', collection.id]);
  }

}
