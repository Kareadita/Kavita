import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CollectionDetailComponent } from './collection-detail/collection-detail.component';
import { SharedModule } from '../shared/shared.module';
import { CollectionsRoutingModule } from './collections-routing.module';
import { CardsModule } from '../cards/cards.module';
import { AllCollectionsComponent } from './all-collections/all-collections.component';



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
  ],
  exports: [
    AllCollectionsComponent
  ]
})
export class CollectionsModule { }
