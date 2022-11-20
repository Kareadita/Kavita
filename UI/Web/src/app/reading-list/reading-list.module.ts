import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DraggableOrderedListComponent } from './draggable-ordered-list/draggable-ordered-list.component';
import { ReadingListDetailComponent } from './reading-list-detail/reading-list-detail.component';
import { ReadingListRoutingModule } from './reading-list-routing.module';
import {DragDropModule} from '@angular/cdk/drag-drop';
import { AddToListModalComponent } from './_modals/add-to-list-modal/add-to-list-modal.component';
import { ReactiveFormsModule } from '@angular/forms';
import { ReadingListsComponent } from './reading-lists/reading-lists.component';
import { EditReadingListModalComponent } from './_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { PipeModule } from '../pipe/pipe.module';
import { SharedModule } from '../shared/shared.module';
import { NgbNavModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import { ReadingListItemComponent } from './reading-list-item/reading-list-item.component';



@NgModule({
  declarations: [
    DraggableOrderedListComponent,
    ReadingListDetailComponent,
    AddToListModalComponent,
    ReadingListsComponent,
    EditReadingListModalComponent,
    ReadingListItemComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DragDropModule,
    NgbNavModule,
    NgbProgressbarModule,
    NgbTooltipModule,

    PipeModule,
    SharedModule,
    SharedSideNavCardsModule,

    ReadingListRoutingModule,
  ],
  exports: [
    AddToListModalComponent,
    ReadingListsComponent,
    EditReadingListModalComponent
  ]
})
export class ReadingListModule { }
