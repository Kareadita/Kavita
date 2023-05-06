import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FilterPipe } from './filter.pipe';
import { PublicationStatusPipe } from './publication-status.pipe';
import { SentenceCasePipe } from './sentence-case.pipe';
import { PersonRolePipe } from './person-role.pipe';
import { SafeHtmlPipe } from './safe-html.pipe';
import { RelationshipPipe } from './relationship.pipe';
import { DefaultValuePipe } from './default-value.pipe';
import { CompactNumberPipe } from './compact-number.pipe';
import { LanguageNamePipe } from './language-name.pipe';
import { AgeRatingPipe } from './age-rating.pipe';
import { MangaFormatPipe } from './manga-format.pipe';
import { MangaFormatIconPipe } from './manga-format-icon.pipe';
import { LibraryTypePipe } from './library-type.pipe';
import { SafeStylePipe } from './safe-style.pipe';
import { DefaultDatePipe } from './default-date.pipe';
import { BytesPipe } from './bytes.pipe';
import { TimeAgoPipe } from './time-ago.pipe';
import { TimeDurationPipe } from './time-duration.pipe';



@NgModule({
  declarations: [
    FilterPipe,
    PersonRolePipe,
    PublicationStatusPipe,
    SentenceCasePipe,
    SafeHtmlPipe,
    RelationshipPipe,
    DefaultValuePipe,
    CompactNumberPipe,
    LanguageNamePipe,
    AgeRatingPipe,
    MangaFormatPipe,
    MangaFormatIconPipe,
    LibraryTypePipe,
    SafeStylePipe,
    DefaultDatePipe,
    BytesPipe,
    TimeAgoPipe,
    TimeDurationPipe,
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
    DefaultValuePipe,
    CompactNumberPipe,
    LanguageNamePipe,
    AgeRatingPipe,
    MangaFormatPipe,
    MangaFormatIconPipe,
    LibraryTypePipe,
    SafeStylePipe,
    DefaultDatePipe,
    BytesPipe,
    TimeAgoPipe,
    TimeDurationPipe
  ]
})
export class PipeModule { }
