import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MetadataFilterComponent } from './metadata-filter.component';
import { NgbCollapseModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../shared/shared.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { FilterComparisonPipe } from './_pipes/filter-comparison.pipe';
import { FilterFieldPipe } from './_pipes/filter-field.pipe';
import { MetadataFilterRowComponent } from './_components/metadata-filter-row/metadata-filter-row.component';
import { MetadataBuilderComponent } from './_components/metadata-builder/metadata-builder.component';
import { MetadataFilterRowGroupComponent } from './_components/metadata-filter-row-group/metadata-filter-row-group.component';
import { CardsModule } from '../cards/cards.module';
import { CardActionablesModule } from '../_single-module/card-actionables/card-actionables.module';

@NgModule({
  declarations: [
    MetadataFilterComponent,
    MetadataFilterRowComponent,
    FilterComparisonPipe,
    FilterFieldPipe,
    MetadataBuilderComponent,
    MetadataFilterRowGroupComponent
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
    CardActionablesModule
  ],
  exports: [
    MetadataFilterComponent
  ]
})
export class MetadataFilterModule { }
