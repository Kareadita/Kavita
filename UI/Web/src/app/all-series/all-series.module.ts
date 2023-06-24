import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllSeriesRoutingModule } from './all-series-routing.module';
import { AllSeriesComponent } from './_components/all-series/all-series.component';
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";



@NgModule({
  declarations: [
    AllSeriesComponent
  ],
  imports: [
    CommonModule,
    AllSeriesRoutingModule,
    SeriesCardComponent,
    BulkOperationsComponent,
    CardDetailLayoutComponent,
    SideNavCompanionBarComponent,
  ]
})
export class AllSeriesModule { }
