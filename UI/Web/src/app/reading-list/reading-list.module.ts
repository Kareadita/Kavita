import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragableOrderedListComponent } from './dragable-ordered-list/dragable-ordered-list.component';
import { ReadingListDetailComponent } from './reading-list-detail/reading-list-detail.component';
import { ReadingListRoutingModule } from './reading-list.router.module';
import {DragDropModule} from '@angular/cdk/drag-drop';
import { AddToListModalComponent } from './_modals/add-to-list-modal/add-to-list-modal.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardsModule } from '../cards/cards.module';
import { ReadingListsComponent } from './reading-lists/reading-lists.component';



@NgModule({
  declarations: [
    DragableOrderedListComponent,
    ReadingListDetailComponent,
    AddToListModalComponent,
    ReadingListsComponent
  ],
  imports: [
    CommonModule,
    ReadingListRoutingModule,
    ReactiveFormsModule,
    DragDropModule,
    CardsModule
  ],
  exports: [
    AddToListModalComponent,
    ReadingListsComponent
  ]
})
export class ReadingListModule { }
