import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DraggableOrderedListComponent } from './_components/draggable-ordered-list/draggable-ordered-list.component';
import { ReadingListRoutingModule } from './reading-list-routing.module';
import {DragDropModule} from '@angular/cdk/drag-drop';
import { AddToListModalComponent } from './_modals/add-to-list-modal/add-to-list-modal.component';
import { ReactiveFormsModule } from '@angular/forms';
import { EditReadingListModalComponent } from './_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { PipeModule } from '../pipe/pipe.module';
import { SharedModule } from '../shared/shared.module';
import { NgbNavModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { SharedSideNavCardsModule } from '../shared-side-nav-cards/shared-side-nav-cards.module';
import { ReadingListDetailComponent } from './_components/reading-list-detail/reading-list-detail.component';
import { ReadingListItemComponent } from './_components/reading-list-item/reading-list-item.component';
import { ReadingListsComponent } from './_components/reading-lists/reading-lists.component';


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
