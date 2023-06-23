import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesDetailRoutingModule } from './series-detail-routing.module';
import { NgbCollapseModule, NgbDropdownModule, NgbNavModule, NgbProgressbarModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { SeriesMetadataDetailComponent } from './_components/series-metadata-detail/series-metadata-detail.component';
import { SharedModule } from '../shared/shared.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { PipeModule } from '../pipe/pipe.module';
import { ReactiveFormsModule } from '@angular/forms';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import { SeriesDetailComponent } from './_components/series-detail/series-detail.component';
import {ReviewCardComponent} from "../_single-module/review-card/review-card.component";
import {CarouselModule} from "../carousel/carousel.module";
import {ExternalRatingComponent} from "./_components/external-rating/external-rating.component";
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";


@NgModule({
  declarations: [
    SeriesDetailComponent,
    SeriesMetadataDetailComponent,
  ],
    imports: [
      CommonModule,
      ReactiveFormsModule,

      NgbCollapseModule, // Series Metadata
      NgbNavModule,
      NgbRatingModule,
      NgbTooltipModule, // Series Detail, Extras Drawer
      NgbProgressbarModule,
      NgbDropdownModule,

      ImageComponent,
      ReadMoreComponent,
      PersonBadgeComponent,

      TypeaheadModule,
      PipeModule,
      SharedModule, // person badge, badge expander (these 2 can be their own module)
      SharedSideNavCardsModule,

      SeriesDetailRoutingModule,
      ReviewCardComponent,
      CarouselModule,
      ExternalRatingComponent
    ]
})
export class SeriesDetailModule { }
