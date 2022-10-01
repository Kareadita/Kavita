import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllSeriesComponent } from './all-series.component';
import { AllSeriesRoutingModule } from './all-series-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';



@NgModule({
  declarations: [
    AllSeriesComponent
  ],
  imports: [
    CommonModule,
    AllSeriesRoutingModule,
    SharedSideNavCardsModule
  ]
})
export class AllSeriesModule { }
