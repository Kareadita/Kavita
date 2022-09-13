import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MetadataFilterComponent } from './metadata-filter.component';
import { NgbCollapseModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../shared/shared.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';

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
    SharedModule,
    TypeaheadModule,
  ],
  exports: [
    MetadataFilterComponent
  ]
})
export class MetadataFilterModule { }
