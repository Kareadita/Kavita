import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardsModule } from '../cards/cards.module';
import { SideNavModule } from '../sidenav/sidenav.module';


/**
 * Exports SideNavModule and CardsModule
 */
@NgModule({
  declarations: [],
  imports: [
    CommonModule,

    CardsModule,
    SideNavModule,
  ],
  exports: [
    CardsModule,
    SideNavModule
  ]
})
export class SharedSideNavCardsModule { }
