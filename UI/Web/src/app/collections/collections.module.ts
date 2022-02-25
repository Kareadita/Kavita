import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CollectionDetailComponent } from './collection-detail/collection-detail.component';
import { SharedModule } from '../shared/shared.module';
import { CollectionsRoutingModule } from './collections-routing.module';
import { CardsModule } from '../cards/cards.module';
import { AllCollectionsComponent } from './all-collections/all-collections.component';
import { SidenavModule } from '../sidenav/sidenav.module';



@NgModule({
  declarations: [
    AllCollectionsComponent,
    CollectionDetailComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    CardsModule,
    CollectionsRoutingModule,
    SidenavModule
  ],
  exports: [
    AllCollectionsComponent
  ]
})
export class CollectionsModule { }
