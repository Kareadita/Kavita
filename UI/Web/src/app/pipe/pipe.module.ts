import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FilterPipe } from './filter.pipe';
import { SentenceCasePipe } from './sentence-case.pipe';
import { SafeHtmlPipe } from './safe-html.pipe';



@NgModule({
  declarations: [
    FilterPipe,
    SentenceCasePipe,
    SafeHtmlPipe
  ],
  imports: [
    CommonModule,
  ],
  exports: [
    FilterPipe,
    SentenceCasePipe,
    SafeHtmlPipe
  ]
})
export class PipeModule { }
