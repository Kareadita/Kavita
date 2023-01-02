import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SortableHeader } from './_directives/sortable-header.directive';



@NgModule({
  declarations: [
    SortableHeader
  ],
  imports: [
    CommonModule
  ],
  exports: [
    SortableHeader
  ]
})
export class TableModule { }
