import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FilterPipe } from './filter.pipe';
import { PersonRolePipe } from './person-role.pipe';
import { PublicationStatusPipe } from './publication-status.pipe';



@NgModule({
  declarations: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe
  ],
  imports: [
    CommonModule,
  ],
  exports: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe
  ]
})
export class PipeModule { }
