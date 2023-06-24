import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesCardComponent } from './series-card/series-card.component';
import { CoverImageChooserComponent } from './cover-image-chooser/cover-image-chooser.component';
import { EditSeriesModalComponent } from './_modals/edit-series-modal/edit-series-modal.component';
import { EditCollectionTagsComponent } from './_modals/edit-collection-tags/edit-collection-tags.component';
import { NgbTooltipModule, NgbCollapseModule, NgbPaginationModule, NgbDropdownModule, NgbProgressbarModule, NgbNavModule, NgbRatingModule, NgbOffcanvasModule } from '@ng-bootstrap/ng-bootstrap';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgxFileDropModule } from 'ngx-file-drop';
import { CardItemComponent } from './card-item/card-item.component';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { CardDetailLayoutComponent } from './card-detail-layout/card-detail-layout.component';
import { BulkOperationsComponent } from './bulk-operations/bulk-operations.component';
import { BulkAddToCollectionComponent } from './_modals/bulk-add-to-collection/bulk-add-to-collection.component';
import { ChapterMetadataDetailComponent } from './chapter-metadata-detail/chapter-metadata-detail.component';
import { MetadataFilterModule } from '../metadata-filter/metadata-filter.module';
import { EditSeriesRelationComponent } from './edit-series-relation/edit-series-relation.component';
import { CardDetailDrawerComponent } from './card-detail-drawer/card-detail-drawer.component';
import { EntityTitleComponent } from './entity-title/entity-title.component';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import { SeriesInfoCardsComponent } from './series-info-cards/series-info-cards.component';
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";
import {UpdateNotificationModalComponent} from "../shared/update-notification/update-notification-modal.component";
import {IconAndTitleComponent} from "../shared/icon-and-title/icon-and-title.component";
import {CardActionablesComponent} from "./card-item/card-actionables/card-actionables.component";
import {DownloadIndicatorComponent} from "./download-indicator/download-indicator.component";
import {AgeRatingPipe} from "../pipe/age-rating.pipe";
import {LanguageNamePipe} from "../pipe/language-name.pipe";
import {DefaultValuePipe} from "../pipe/default-value.pipe";
import {PublicationStatusPipe} from "../pipe/publication-status.pipe";
import {MangaFormatIconPipe} from "../pipe/manga-format-icon.pipe";
import {MangaFormatPipe} from "../pipe/manga-format.pipe";
import {TimeAgoPipe} from "../pipe/time-ago.pipe";
import {CompactNumberPipe} from "../pipe/compact-number.pipe";
import {RelationshipPipe} from "../pipe/relationship.pipe";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {BytesPipe} from "../pipe/bytes.pipe";
import {DefaultDatePipe} from "../pipe/default-date.pipe";
import {EntityInfoCardsComponent} from "./entity-info-cards/entity-info-cards.component";
import {FilterPipe} from "../pipe/filter.pipe";



@NgModule({
  declarations: [
    CardItemComponent,
    SeriesCardComponent,
    CoverImageChooserComponent,
    EditSeriesModalComponent,
    EditCollectionTagsComponent,
    CardDetailLayoutComponent,
    BulkOperationsComponent,
    BulkAddToCollectionComponent,
    ChapterMetadataDetailComponent,
    EditSeriesRelationComponent,
    CardDetailDrawerComponent,
    EntityTitleComponent,
    SeriesInfoCardsComponent,
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    FormsModule, // EditCollectionsModal

    SharedModule,
    TypeaheadModule, // edit series modal

    ImageComponent,
    ReadMoreComponent,
    BadgeExpanderComponent,
    PersonBadgeComponent,
    UpdateNotificationModalComponent,

    MetadataFilterModule,

    NgbTooltipModule, // Card item
    NgbCollapseModule,
    NgbRatingModule,

    VirtualScrollerModule,

    NgbOffcanvasModule, // Series Detail, action of cards
    NgbNavModule, //Series Detail
    NgbPaginationModule, // EditCollectionTagsComponent
    NgbDropdownModule,
    NgbProgressbarModule,
    NgxFileDropModule, // Cover Chooser


    SharedModule,
    IconAndTitleComponent,
    CardActionablesComponent,
    DownloadIndicatorComponent,
    AgeRatingPipe,
    LanguageNamePipe,
    DefaultValuePipe,
    PublicationStatusPipe,
    MangaFormatIconPipe,
    MangaFormatPipe,
    TimeAgoPipe,
    CompactNumberPipe,
    RelationshipPipe,
    SentenceCasePipe,
    BytesPipe,
    DefaultDatePipe,
    EntityInfoCardsComponent,
    FilterPipe,
  ],
  exports: [
    CardItemComponent,
    SeriesCardComponent,
    SeriesCardComponent,
    CoverImageChooserComponent,
    EditSeriesModalComponent,
    EditCollectionTagsComponent,
    CardDetailLayoutComponent,
    BulkOperationsComponent,
    ChapterMetadataDetailComponent,
    EditSeriesRelationComponent,

    EntityTitleComponent,

    NgbOffcanvasModule,

    VirtualScrollerModule,
    SeriesInfoCardsComponent,
  ]
})
export class CardsModule { }
