import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllSeriesRoutingModule } from './all-series-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import { AllSeriesComponent } from './_components/all-series/all-series.component';



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
