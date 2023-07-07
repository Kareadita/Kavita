import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CollectionDetailComponent } from './_components/collection-detail/collection-detail.component';
import { AllCollectionsComponent } from './_components/all-collections/all-collections.component';
import { CollectionsRoutingModule } from './collections-routing.module';
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {CardItemComponent} from "../cards/card-item/card-item.component";



@NgModule({
    imports: [
        CommonModule,
        ImageComponent,
        ReadMoreComponent,
        CollectionsRoutingModule,
        CardActionablesComponent,
        SideNavCompanionBarComponent,
        BulkOperationsComponent,
        CardDetailLayoutComponent,
        SeriesCardComponent,
        CardItemComponent,
        AllCollectionsComponent,
        CollectionDetailComponent,
    ],
    exports: [
        AllCollectionsComponent
    ]
})
export class CollectionsModule { }
