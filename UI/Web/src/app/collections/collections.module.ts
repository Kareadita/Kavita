import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CollectionDetailComponent } from './collection-detail/collection-detail.component';
import { SharedModule } from '../shared/shared.module';
import { AllCollectionsComponent } from './all-collections/all-collections.component';
import { CollectionsRoutingModule } from './collections-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';



@NgModule({
  declarations: [
    AllCollectionsComponent,
    CollectionDetailComponent
  ],
  imports: [
    CommonModule,
    SharedModule,

    SharedSideNavCardsModule,

    CollectionsRoutingModule,
  ],
  exports: [
    AllCollectionsComponent
  ]
})
export class CollectionsModule { }
