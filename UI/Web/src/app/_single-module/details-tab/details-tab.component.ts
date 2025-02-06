import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasCast} from "../../_models/common/i-has-cast";
import {PersonRole} from "../../_models/metadata/person";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {Genre} from "../../_models/metadata/genre";
import {Tag} from "../../_models/tag";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {ImageComponent} from "../../shared/image/image.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {ImageService} from "../../_services/image.service";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {IHasReadingTime} from "../../_models/common/i-has-reading-time";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {MangaFormat} from "../../_models/manga-format";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {LanguageNamePipe} from "../../_pipes/language-name.pipe";
import {AsyncPipe} from "@angular/common";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";

@Component({
  selector: 'app-details-tab',
  standalone: true,
  imports: [
    CarouselReelComponent,
    PersonBadgeComponent,
    TranslocoDirective,
    TagBadgeComponent,
    ImageComponent,
    SafeHtmlPipe,
    BadgeExpanderComponent,
    ReadTimePipe,
    SentenceCasePipe,
    SeriesFormatComponent,
    MangaFormatPipe,
    LanguageNamePipe,
    AsyncPipe,
    SafeUrlPipe
  ],
  templateUrl: './details-tab.component.html',
  styleUrl: './details-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DetailsTabComponent {

  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);

  protected readonly PersonRole = PersonRole;
  protected readonly FilterField = FilterField;

  @Input({required: true}) metadata!: IHasCast;
  @Input() readingTime: IHasReadingTime | undefined;
  @Input() language: string | undefined;
  @Input() format: MangaFormat = MangaFormat.UNKNOWN;
  @Input() releaseYear: number | undefined;
  @Input() genres: Array<Genre> = [];
  @Input() tags: Array<Tag> = [];
  @Input() webLinks: Array<string> = [];


  openGeneric(queryParamName: FilterField, filter: string | number) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }

  protected readonly MangaFormat = MangaFormat;
}
