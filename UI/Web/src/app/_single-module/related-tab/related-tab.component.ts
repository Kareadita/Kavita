import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {ReadingList} from "../../_models/reading-list";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ImageService} from "../../_services/image.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {UserCollection} from "../../_models/collection-tag";
import {Series} from "../../_models/series";
import {RelationKind} from "../../_models/series-detail/relation-kind";
import {PageBookmark} from "../../_models/readers/page-bookmark";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {EntityCardComponent} from "../../cards/entity-card/entity-card.component";
import {CardEntityFactory} from "../../_models/card/card-entity";

export interface RelatedSeriesPair {
  series: Series;
  relation: RelationKind;
}

@Component({
  selector: 'app-related-tab',
  imports: [
    CarouselReelComponent,
    TranslocoDirective,
    EntityCardComponent
  ],
  templateUrl: './related-tab.component.html',
  styleUrl: './related-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RelatedTabComponent {

  protected readonly imageService = inject(ImageService);
  private readonly cardConfigFactory = inject(CardConfigFactory);

  readingLists = input<ReadingList[]>([]);
  readingListEntities = computed(() => this.readingLists().map(r => CardEntityFactory.readingList(r)));
  readingListConfig = computed(() => this.cardConfigFactory.forReadingList());

  collections = input<UserCollection[]>([]);
  collectionEntities = computed(() => this.collections().map(c => CardEntityFactory.collection(c)));
  collectionConfig = computed(() => this.cardConfigFactory.forCollection())


  bookmarks = input<PageBookmark[]>([]);
  bookmarkEntities = computed(() => this.bookmarks().map(b => CardEntityFactory.bookmark(b)));
  bookmarkConfig = computed(() => {
    return this.cardConfigFactory.forBookmark(undefined, {
      titleFunc: (d) => translate('related-tab.bookmarks-title'),
      coverFunc: (d) => this.imageService.getSeriesCoverImage(d.seriesId),
      metaTitleFunc: d => '',
      allowSelection: false
    });
  })

  relations = input<RelatedSeriesPair[]>([]);
  relationEntities = computed(() => this.relations().map(r => CardEntityFactory.related(r)));
  relatedConfig = computed(() => this.cardConfigFactory.forRelationship());
}
