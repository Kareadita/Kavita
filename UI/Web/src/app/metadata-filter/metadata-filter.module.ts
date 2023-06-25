import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MetadataFilterComponent } from './metadata-filter.component';
import { NgbCollapseModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import {DrawerComponent} from "../shared/drawer/drawer.component";
import {TypeaheadComponent} from "../typeahead/_components/typeahead.component";

@NgModule({
  declarations: [
    MetadataFilterComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbTooltipModule,
    NgbRatingModule,
    NgbCollapseModule,
    DrawerComponent,
    TypeaheadComponent,
  ],
  exports: [
    MetadataFilterComponent
  ]
})
export class MetadataFilterModule { }
