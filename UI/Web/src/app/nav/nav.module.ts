import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import { EventsWidgetComponent } from './_components/events-widget/events-widget.component';
import { NgbDropdownModule, NgbPopoverModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { SharedModule } from '../shared/shared.module';
import { PipeModule } from '../pipe/pipe.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { GroupedTypeaheadComponent } from './_components/grouped-typeahead/grouped-typeahead.component';
import { NavHeaderComponent } from './_components/nav-header/nav-header.component';
import {ImageComponent} from "../shared/image/image.component";



@NgModule({
  declarations: [
    NavHeaderComponent,
    EventsWidgetComponent,
    GroupedTypeaheadComponent,
  ],
    imports: [
        CommonModule,
        ReactiveFormsModule,
        RouterModule,

        NgbDropdownModule,
        NgbPopoverModule,
        NgbNavModule,

        SharedModule, // app image, series-format
        PipeModule,
        TypeaheadModule,
        NgOptimizedImage,
        ImageComponent,
    ],
  exports: [
    NavHeaderComponent,
    SharedModule
  ]
})
export class NavModule { }
