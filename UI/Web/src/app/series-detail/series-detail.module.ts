import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesDetailRoutingModule } from './series-detail-routing.module';
import { ReactiveFormsModule } from '@angular/forms';
import { SeriesDetailComponent } from './_components/series-detail/series-detail.component';
import {ReviewCardComponent} from "../_single-module/review-card/review-card.component";

import {ExternalRatingComponent} from "./_components/external-rating/external-rating.component";
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";
import {IconAndTitleComponent} from "../shared/icon-and-title/icon-and-title.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {ExternalSeriesCardComponent} from "../cards/external-series-card/external-series-card.component";
import {ExternalListItemComponent} from "../cards/external-list-item/external-list-item.component";
import {ListItemComponent} from "../cards/list-item/list-item.component";
import {SafeHtmlPipe} from "../pipe/safe-html.pipe";
import {TagBadgeComponent} from "../shared/tag-badge/tag-badge.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {EntityTitleComponent} from "../cards/entity-title/entity-title.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {SeriesMetadataDetailComponent} from "./_components/series-metadata-detail/series-metadata-detail.component";
import {
  NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbProgressbar,
  NgbTooltipModule
} from "@ng-bootstrap/ng-bootstrap";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";


@NgModule({
    imports: [
    CommonModule,
    ReactiveFormsModule,
    SeriesDetailRoutingModule,
    ImageComponent,
    ReadMoreComponent,
    PersonBadgeComponent,
    IconAndTitleComponent,
    BadgeExpanderComponent,
    ExternalSeriesCardComponent,
    ExternalListItemComponent,
    ListItemComponent,
    ReviewCardComponent,
    ExternalRatingComponent,
    CardActionablesComponent,
    SafeHtmlPipe,
    TagBadgeComponent,
    LoadingComponent,
    VirtualScrollerModule,
    CardItemComponent,
    SeriesCardComponent,
    EntityTitleComponent,
    BulkOperationsComponent,
    SeriesMetadataDetailComponent,
    NgbNavOutlet,
    NgbNavItem,
    NgbNavLink,
    NgbNavContent,
    SideNavCompanionBarComponent,
    NgbNav,
    NgbProgressbar,
    NgbTooltipModule,
    NgbDropdown,
    NgbDropdownItem,
    NgbDropdownMenu,
    NgbDropdownToggle,
    SeriesDetailComponent,
]
})
export class SeriesDetailModule { }
