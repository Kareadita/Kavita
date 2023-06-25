import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import { EventsWidgetComponent } from './_components/events-widget/events-widget.component';
import { NgbDropdownModule, NgbPopoverModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { GroupedTypeaheadComponent } from './_components/grouped-typeahead/grouped-typeahead.component';
import { NavHeaderComponent } from './_components/nav-header/nav-header.component';
import {ImageComponent} from "../shared/image/image.component";
import {CircularLoaderComponent} from "../shared/circular-loader/circular-loader.component";
import {PersonRolePipe} from "../pipe/person-role.pipe";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {SeriesFormatComponent} from "../shared/series-format/series-format.component";



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

    NgOptimizedImage,
    ImageComponent,
    CircularLoaderComponent,
    PersonRolePipe,
    SentenceCasePipe,
    SeriesFormatComponent,
  ],
  exports: [
    NavHeaderComponent,
  ]
})
export class NavModule { }
