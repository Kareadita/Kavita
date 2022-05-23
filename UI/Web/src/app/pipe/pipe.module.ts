import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FilterPipe } from './filter.pipe';
import { PublicationStatusPipe } from './publication-status.pipe';
import { SentenceCasePipe } from './sentence-case.pipe';
import { PersonRolePipe } from './person-role.pipe';
import { SafeHtmlPipe } from './safe-html.pipe';
import { RelationshipPipe } from './relationship.pipe';
import { DefaultValuePipe } from './default-value.pipe';



@NgModule({
  declarations: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe,
    SentenceCasePipe,
    SafeHtmlPipe,
    RelationshipPipe,
    DefaultValuePipe
  ],
  imports: [
    CommonModule,
  ],
  exports: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe,
    SentenceCasePipe,
    SafeHtmlPipe,
    RelationshipPipe,
    DefaultValuePipe
  ]
})
export class PipeModule { }
