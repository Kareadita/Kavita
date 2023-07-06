import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DraggableOrderedListComponent } from './_components/draggable-ordered-list/draggable-ordered-list.component';
import { ReadingListRoutingModule } from './reading-list-routing.module';
import {DragDropModule} from '@angular/cdk/drag-drop';
import { AddToListModalComponent } from './_modals/add-to-list-modal/add-to-list-modal.component';
import { ReactiveFormsModule } from '@angular/forms';
import { EditReadingListModalComponent } from './_modals/edit-reading-list-modal/edit-reading-list-modal.component';
import { NgbDropdownModule, NgbNavModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReadingListDetailComponent } from './_components/reading-list-detail/reading-list-detail.component';
import { ReadingListItemComponent } from './_components/reading-list-item/reading-list-item.component';
import { ReadingListsComponent } from './_components/reading-lists/reading-lists.component';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import {ImageComponent} from "../shared/image/image.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {PersonBadgeComponent} from "../shared/person-badge/person-badge.component";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";
import {MangaFormatPipe} from "../pipe/manga-format.pipe";
import {MangaFormatIconPipe} from "../pipe/manga-format-icon.pipe";
import {SafeHtmlPipe} from "../pipe/safe-html.pipe";
import {FilterPipe} from "../pipe/filter.pipe";
import {CoverImageChooserComponent} from "../cards/cover-image-chooser/cover-image-chooser.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {A11yClickDirective} from "../shared/a11y-click.directive";

@NgModule({
  declarations: [
    DraggableOrderedListComponent,
    ReadingListDetailComponent,
    AddToListModalComponent,
    ReadingListsComponent,
    EditReadingListModalComponent,
    ReadingListItemComponent,
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DragDropModule,
    NgbNavModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    NgbDropdownModule,

    ImageComponent,
    ReadMoreComponent,
    PersonBadgeComponent,
    BadgeExpanderComponent,

    ReadingListRoutingModule,

    VirtualScrollerModule,
    CardActionablesComponent,
    MangaFormatPipe,
    MangaFormatIconPipe,
    SafeHtmlPipe,
    FilterPipe,
    CoverImageChooserComponent,
    CardDetailLayoutComponent,
    CardItemComponent,
    SideNavCompanionBarComponent,
    LoadingComponent,
    A11yClickDirective,
  ],
  exports: [
    AddToListModalComponent,
    ReadingListsComponent,
    EditReadingListModalComponent
  ]
})
export class ReadingListModule { }
