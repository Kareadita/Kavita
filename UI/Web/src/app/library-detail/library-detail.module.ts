import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardModule } from '../dashboard/dashboard.module';
import { CardsModule } from '../cards/cards.module';
import { SidenavModule } from '../sidenav/sidenav.module';
import { LibraryDetailComponent } from './library-detail.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { PipeModule } from '../pipe/pipe.module';
import { LibraryDetailRoutingModule } from './library-detail-routing.module';



@NgModule({
  declarations: [LibraryDetailComponent],
  imports: [
    CommonModule,

    NgbNavModule,

    PipeModule,

    CardsModule,
    SidenavModule,

    //DashboardModule, // Temp this needs the dashboard until we have better Recommended screen
    
    LibraryDetailRoutingModule
  ]
})
export class LibraryDetailModule { }
