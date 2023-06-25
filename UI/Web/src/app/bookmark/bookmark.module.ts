import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookmarkRoutingModule } from './bookmark-routing.module';
import { BookmarksComponent } from './_components/bookmarks/bookmarks.component';
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardItemComponent} from "../cards/card-item/card-item.component";



@NgModule({
  declarations: [
    BookmarksComponent
  ],
  imports: [
    CommonModule,
    BookmarkRoutingModule,
    BulkOperationsComponent,
    CardDetailLayoutComponent,
    SideNavCompanionBarComponent,
    CardItemComponent
  ]
})
export class BookmarkModule { }
