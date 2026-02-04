import {ChangeDetectionStrategy, Component, inject, input, Input} from '@angular/core';
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
import {PageBookmark} from "../../_models/readers/page-bookmark";
import {CardConfigFactory} from "../../_services/card-config-factory.service";

export interface RelatedSeriesPair {
  series: Series;
  relation: RelationKind;
}

@Component({
    selector: 'app-related-tab',
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
  private readonly cardConfigFactory = inject(CardConfigFactory);

  @Input() readingLists: Array<ReadingList> = [];
  @Input() collections: Array<UserCollection> = [];
  @Input() relations: Array<RelatedSeriesPair> = [];
  bookmarks = input<PageBookmark[]>([]);
  @Input() libraryId!: number;

  //bookmarkEntries = computed(() => this.bookmarks().map(c => CardEntityFactory.series(c)))

  openReadingList(readingList: ReadingList) {
    this.router.navigate(['lists', readingList.id]);
  }

  openCollection(collection: UserCollection) {
    this.router.navigate(['collections', collection.id]);
  }

  viewBookmark(bookmark: PageBookmark) {
    this.router.navigate(['library', this.libraryId, 'series', bookmark.seriesId, 'manga', 0], {queryParams: {incognitoMode: false, bookmarkMode: true}});
  }

}
