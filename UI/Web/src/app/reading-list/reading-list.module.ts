import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragableOrderedListComponent } from './dragable-ordered-list/dragable-ordered-list.component';
import { ReadingListDetailComponent } from './reading-list-detail/reading-list-detail.component';
import { ReadingListRoutingModule } from './reading-list.router.module';
import {DragDropModule} from '@angular/cdk/drag-drop';



@NgModule({
  declarations: [
    DragableOrderedListComponent,
    ReadingListDetailComponent
  ],
  imports: [
    CommonModule,
    ReadingListRoutingModule,
    DragDropModule,
  ]
})
export class ReadingListModule { }
