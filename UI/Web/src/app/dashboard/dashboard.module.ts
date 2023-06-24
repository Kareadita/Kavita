
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardRoutingModule } from './dashboard-routing.module';

import { CarouselModule } from '../carousel/carousel.module';
import { DashboardComponent } from './_components/dashboard.component';
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";


@NgModule({
  declarations: [DashboardComponent],
  imports: [
    CommonModule,
    CarouselModule,
    DashboardRoutingModule,
    CardItemComponent,
    SeriesCardComponent,
    SideNavCompanionBarComponent
  ]
})
export class DashboardModule { }
