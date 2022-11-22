import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardRoutingModule } from './dashboard-routing.module';

import { CarouselModule } from '../carousel/carousel.module';
import { DashboardComponent } from './_components/dashboard.component';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';


@NgModule({
  declarations: [DashboardComponent],
  imports: [
    CommonModule,
    CarouselModule,
    SharedSideNavCardsModule,
    DashboardRoutingModule
  ]
})
export class DashboardModule { }
