import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragableOrderedListComponent } from './dragable-ordered-list/dragable-ordered-list.component';
import { ReadingListDetailComponent } from './reading-list-detail/reading-list-detail.component';
import { ReadingListRoutingModule } from './reading-list.router.module';
import {DragDropModule} from '@angular/cdk/drag-drop';
import { AddToListModalComponent } from './_modals/add-to-list-modal/add-to-list-modal.component';



@NgModule({
  declarations: [
    DragableOrderedListComponent,
    ReadingListDetailComponent,
    AddToListModalComponent
  ],
  imports: [
    CommonModule,
    ReadingListRoutingModule,
    DragDropModule,
  ],
  exports: [
    AddToListModalComponent
  ]
})
export class ReadingListModule { }
