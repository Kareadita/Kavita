import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CollectionDetailComponent } from './_components/collection-detail/collection-detail.component';
import { SharedModule } from '../shared/shared.module';
import { AllCollectionsComponent } from './_components/all-collections/all-collections.component';
import { CollectionsRoutingModule } from './collections-routing.module';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";



@NgModule({
  declarations: [
    AllCollectionsComponent,
    CollectionDetailComponent
  ],
    imports: [
        CommonModule,
        SharedModule,

        SharedSideNavCardsModule,

        ImageComponent,
        ReadMoreComponent,

        CollectionsRoutingModule,
        CardActionablesComponent,
    ],
  exports: [
    AllCollectionsComponent
  ]
})
export class CollectionsModule { }
