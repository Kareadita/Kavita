import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EventsWidgetComponent } from './events-widget/events-widget.component';
import { GroupedTypeaheadComponent } from './grouped-typeahead/grouped-typeahead.component';
import { NavHeaderComponent } from './nav-header/nav-header.component';
import { NgbDropdownModule, NgbPopoverModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { SharedModule } from '../shared/shared.module';
import { PipeModule } from '../pipe/pipe.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { ReactiveFormsModule } from '@angular/forms';



@NgModule({
  declarations: [
    NavHeaderComponent,
    EventsWidgetComponent,
    GroupedTypeaheadComponent,
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,

    NgbDropdownModule,
    NgbPopoverModule,
    NgbNavModule,

    SharedModule, // app image, series-format
    PipeModule,
    TypeaheadModule,
  ],
  exports: [
    NavHeaderComponent,
    SharedModule
  ]
})
export class NavModule { }
