import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardsModule } from '../cards/cards.module';
import { SidenavModule } from '../sidenav/sidenav.module';
import { DashboardRoutingModule } from './dashboard-routing.module';

import { CarouselModule } from '../carousel/carousel.module';
import { DashboardComponent } from './dashboard.component';



@NgModule({
  declarations: [DashboardComponent],
  imports: [
    CommonModule,

    CarouselModule,

    CardsModule,
    SidenavModule,

    DashboardRoutingModule
  ]
})
export class DashboardModule { }
