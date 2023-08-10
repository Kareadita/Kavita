
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardRoutingModule } from './dashboard-routing.module';


import { DashboardComponent } from './_components/dashboard.component';
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {TranslocoModule} from "@ngneat/transloco";


@NgModule({
    imports: [
    CommonModule,
    DashboardRoutingModule,
    CardItemComponent,
    SeriesCardComponent,
    SideNavCompanionBarComponent,
    DashboardComponent,
    TranslocoModule
]
})
export class DashboardModule { }
