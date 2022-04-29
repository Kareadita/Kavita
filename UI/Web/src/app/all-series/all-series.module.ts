import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllSeriesComponent } from './all-series.component';
import { AllSeriesRoutingModule } from './all-series-routing.module';
import { SidenavModule } from '../sidenav/sidenav.module';
import { CardsModule } from '../cards/cards.module';



@NgModule({
  declarations: [
    AllSeriesComponent
  ],
  imports: [
    CommonModule,
    AllSeriesRoutingModule,

    SidenavModule,
    CardsModule

  ]
})
export class AllSeriesModule { }
