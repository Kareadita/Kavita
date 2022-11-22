import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookmarkRoutingModule } from './bookmark-routing.module';
import { BookmarksComponent } from './_components/bookmarks/bookmarks.component';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';



@NgModule({
  declarations: [
    BookmarksComponent
  ],
  imports: [
    CommonModule,
    
    SharedSideNavCardsModule,

    BookmarkRoutingModule
  ]
})
export class BookmarkModule { }
