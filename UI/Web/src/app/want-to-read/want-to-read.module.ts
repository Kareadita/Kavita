import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WantToReadComponent } from './_components/want-to-read/want-to-read.component';
import { WantToReadRoutingModule } from './want-to-read-routing.module';
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";



@NgModule({
  declarations: [
    WantToReadComponent
  ],
  imports: [
    CommonModule,
    WantToReadRoutingModule,
    BulkOperationsComponent,
    CardDetailLayoutComponent,
    SeriesCardComponent,
    SideNavCompanionBarComponent
  ]
})
export class WantToReadModule { }
