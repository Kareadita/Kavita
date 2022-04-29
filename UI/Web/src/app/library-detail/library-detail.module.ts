import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LibraryDetailComponent } from './library-detail.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { PipeModule } from '../pipe/pipe.module';
import { LibraryDetailRoutingModule } from './library-detail-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';



@NgModule({
  declarations: [LibraryDetailComponent],
  imports: [
    CommonModule,

    NgbNavModule,

    PipeModule,
    SharedSideNavCardsModule,

    //DashboardModule, // Temp this needs the dashboard until we have better Recommended screen
    
    LibraryDetailRoutingModule
  ]
})
export class LibraryDetailModule { }
