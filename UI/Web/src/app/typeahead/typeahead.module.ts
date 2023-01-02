import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TypeaheadComponent } from './_components/typeahead.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../shared/shared.module';
import { PipeModule } from '../pipe/pipe.module';




@NgModule({
  declarations: [
    TypeaheadComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    FormsModule,
    ReactiveFormsModule,
    PipeModule
  ],
  exports: [
    TypeaheadComponent
  ]
})
export class TypeaheadModule { }
