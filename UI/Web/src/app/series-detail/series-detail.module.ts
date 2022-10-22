import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesDetailRoutingModule } from './series-detail-routing.module';
import { NgbCollapseModule, NgbNavModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { SeriesDetailComponent } from './series-detail.component';
import { SeriesMetadataDetailComponent } from './series-metadata-detail/series-metadata-detail.component';
import { ReviewSeriesModalComponent } from './review-series-modal/review-series-modal.component';
import { SharedModule } from '../shared/shared.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { PipeModule } from '../pipe/pipe.module';
import { ReactiveFormsModule } from '@angular/forms';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';


@NgModule({
  declarations: [
    SeriesDetailComponent,
    ReviewSeriesModalComponent,
    SeriesMetadataDetailComponent,
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule, // Review Series Modal

    NgbCollapseModule, // Series Metadata
    NgbNavModule,
    NgbRatingModule,
    NgbTooltipModule, // Series Detail, Extras Drawer

    TypeaheadModule,
    PipeModule,
    SharedModule, // person badge, badge expander (these 2 can be their own module)
    SharedSideNavCardsModule,

    SeriesDetailRoutingModule
  ]
})
export class SeriesDetailModule { }
