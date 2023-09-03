import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LibraryDetailComponent } from './library-detail.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryDetailRoutingModule } from './library-detail-routing.module';
import { LibraryRecommendedComponent } from './library-recommended/library-recommended.component';

import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";



@NgModule({
    imports: [
    CommonModule,
    NgbNavModule,
    LibraryDetailRoutingModule,
    CardActionablesComponent,
    SentenceCasePipe,
    CardDetailLayoutComponent,
    SeriesCardComponent,
    BulkOperationsComponent,
    SideNavCompanionBarComponent,
    LibraryDetailComponent, LibraryRecommendedComponent
]
})
export class LibraryDetailModule { }
