import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LibraryDetailComponent } from './library-detail.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryDetailRoutingModule } from './library-detail-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import { LibraryRecommendedComponent } from './library-recommended/library-recommended.component';
import { CarouselModule } from '../carousel/carousel.module';
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";



@NgModule({
  declarations: [LibraryDetailComponent, LibraryRecommendedComponent],
  imports: [
    CommonModule,

    NgbNavModule,

    CarouselModule, // because this is heavy, we might want recommended in a new url

    SharedSideNavCardsModule,

    LibraryDetailRoutingModule,
    CardActionablesComponent,
    SentenceCasePipe
  ]
})
export class LibraryDetailModule { }
