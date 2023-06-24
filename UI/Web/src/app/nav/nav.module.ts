import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import { EventsWidgetComponent } from './_components/events-widget/events-widget.component';
import { NgbDropdownModule, NgbPopoverModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { SharedModule } from '../shared/shared.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { GroupedTypeaheadComponent } from './_components/grouped-typeahead/grouped-typeahead.component';
import { NavHeaderComponent } from './_components/nav-header/nav-header.component';
import {ImageComponent} from "../shared/image/image.component";
import {CircularLoaderComponent} from "../shared/circular-loader/circular-loader.component";
import {PersonRolePipe} from "../pipe/person-role.pipe";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";



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
    TypeaheadModule,
    NgOptimizedImage,
    ImageComponent,
    CircularLoaderComponent,
    PersonRolePipe,
    SentenceCasePipe,
  ],
  exports: [
    NavHeaderComponent,
    SharedModule
  ]
})
export class NavModule { }
