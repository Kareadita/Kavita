import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FilterPipe } from './filter.pipe';
import { PublicationStatusPipe } from './publication-status.pipe';
import { SentenceCasePipe } from './sentence-case.pipe';
import { PersonRolePipe } from './person-role.pipe';
import { SafeHtmlPipe } from './safe-html.pipe';



@NgModule({
  declarations: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe,
    SentenceCasePipe,
    SafeHtmlPipe
  ],
  imports: [
    CommonModule,
  ],
  exports: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe,
    SentenceCasePipe,
    SafeHtmlPipe
  ]
})
export class PipeModule { }
