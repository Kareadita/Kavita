import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesCardComponent } from './series-card/series-card.component';
import { CoverImageChooserComponent } from './cover-image-chooser/cover-image-chooser.component';
import { EditSeriesModalComponent } from './_modals/edit-series-modal/edit-series-modal.component';
import { EditCollectionTagsComponent } from './_modals/edit-collection-tags/edit-collection-tags.component';
import { NgbTooltipModule, NgbCollapseModule, NgbPaginationModule, NgbDropdownModule, NgbProgressbarModule, NgbNavModule, NgbRatingModule, NgbOffcanvasModule } from '@ng-bootstrap/ng-bootstrap';
import { CardActionablesComponent } from './card-item/card-actionables/card-actionables.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgxFileDropModule } from 'ngx-file-drop';
import { CardItemComponent } from './card-item/card-item.component';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { CardDetailLayoutComponent } from './card-detail-layout/card-detail-layout.component';
import { BulkOperationsComponent } from './bulk-operations/bulk-operations.component';
import { BulkAddToCollectionComponent } from './_modals/bulk-add-to-collection/bulk-add-to-collection.component';
import { PipeModule } from '../pipe/pipe.module';
import { ChapterMetadataDetailComponent } from './chapter-metadata-detail/chapter-metadata-detail.component';
import { MetadataFilterModule } from '../metadata-filter/metadata-filter.module';
import { EditSeriesRelationComponent } from './edit-series-relation/edit-series-relation.component';
import { CardDetailDrawerComponent } from './card-detail-drawer/card-detail-drawer.component';
import { EntityTitleComponent } from './entity-title/entity-title.component';
import { EntityInfoCardsComponent } from './entity-info-cards/entity-info-cards.component';
import { ListItemComponent } from './list-item/list-item.component';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import { SeriesInfoCardsComponent } from './series-info-cards/series-info-cards.component';
import { DownloadIndicatorComponent } from './download-indicator/download-indicator.component';
import { DynamicListPipe } from './dynamic-list.pipe';
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";
import {UpdateNotificationModalComponent} from "../shared/update-notification/update-notification-modal.component";
import {IconAndTitleComponent} from "../shared/icon-and-title/icon-and-title.component";



@NgModule({
  declarations: [
    CardItemComponent,
    SeriesCardComponent,
    CoverImageChooserComponent,
    EditSeriesModalComponent,
    EditCollectionTagsComponent,
    CardActionablesComponent,
    CardDetailLayoutComponent,
    BulkOperationsComponent,
    BulkAddToCollectionComponent,
    ChapterMetadataDetailComponent,
    EditSeriesRelationComponent,
    CardDetailDrawerComponent,
    EntityTitleComponent,
    EntityInfoCardsComponent,
    ListItemComponent,
    SeriesInfoCardsComponent,
    DownloadIndicatorComponent,
    DynamicListPipe,
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    FormsModule, // EditCollectionsModal

    PipeModule,
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
    PipeModule, // filter for BulkAddToCollectionComponent


    SharedModule,
    IconAndTitleComponent,
    // IconAndTitleComponent
  ],
  exports: [
    CardItemComponent,
    SeriesCardComponent,
    SeriesCardComponent,
    CoverImageChooserComponent,
    EditSeriesModalComponent,
    EditCollectionTagsComponent,
    CardActionablesComponent,
    CardDetailLayoutComponent,
    BulkOperationsComponent,
    ChapterMetadataDetailComponent,
    EditSeriesRelationComponent,

    EntityTitleComponent,
    EntityInfoCardsComponent,
    ListItemComponent,

    NgbOffcanvasModule,

    VirtualScrollerModule,
    SeriesInfoCardsComponent
  ]
})
export class CardsModule { }
