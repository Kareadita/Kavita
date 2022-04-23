import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardsModule } from '../cards/cards.module';
import { SharedModule } from '../shared/shared.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { SidenavModule } from '../sidenav/sidenav.module';
import { BookmarkRoutingModule } from './bookmark-routing.module';
import { BookmarksComponent } from './bookmarks/bookmarks.component';



@NgModule({
  declarations: [
    BookmarksComponent
  ],
  imports: [
    CommonModule,
    CardsModule,
    SharedModule,
    SidenavModule,
    NgbTooltipModule,

    BookmarkRoutingModule
  ]
})
export class BookmarkModule { }
