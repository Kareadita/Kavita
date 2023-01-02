import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WantToReadComponent } from './_components/want-to-read/want-to-read.component';
import { CardsModule } from '../cards/cards.module';
import { SidenavModule } from '../sidenav/sidenav.module';
import { WantToReadRoutingModule } from './want-to-read-routing.module';



@NgModule({
  declarations: [
    WantToReadComponent
  ],
  imports: [
    CommonModule,
    CardsModule,
    SidenavModule,
    WantToReadRoutingModule
  ]
})
export class WantToReadModule { }
