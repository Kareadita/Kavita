import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardsModule } from '../cards/cards.module';
import { SidenavModule } from '../sidenav/sidenav.module';


/**
 * Exports SideNavModule and CardsModule
 */
@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    CardsModule,
    SidenavModule,
  ],
  exports: [
    CardsModule,
    SidenavModule
  ]
})
export class SharedSideNavCardsModule { }
