import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LibraryDetailComponent } from './library-detail.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryDetailRoutingModule } from './library-detail-routing.module';
import { LibraryRecommendedComponent } from './library-recommended/library-recommended.component';
import { CarouselModule } from '../carousel/carousel.module';
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";



@NgModule({
  declarations: [LibraryDetailComponent, LibraryRecommendedComponent],
  imports: [
    CommonModule,

    NgbNavModule,

    CarouselModule, // because this is heavy, we might want recommended in a new url

    LibraryDetailRoutingModule,
    CardActionablesComponent,
    SentenceCasePipe,
    CardDetailLayoutComponent,
    SeriesCardComponent,
    BulkOperationsComponent,
    SideNavCompanionBarComponent
  ]
})
export class LibraryDetailModule { }
