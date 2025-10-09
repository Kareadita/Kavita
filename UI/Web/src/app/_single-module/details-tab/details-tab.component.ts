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
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {IHasReadingTime} from "../../_models/common/i-has-reading-time";
import {ReadTimePipe} from "../../_pipes/read-time.pipe";
import {MangaFormat} from "../../_models/manga-format";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {LanguageNamePipe} from "../../_pipes/language-name.pipe";
import {AsyncPipe} from "@angular/common";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";
import {AgeRating} from "../../_models/metadata/age-rating";
import {AgeRatingImageComponent} from "../age-rating-image/age-rating-image.component";
import {AccountService} from "../../_services/account.service";

@Component({
  selector: 'app-details-tab',
  imports: [
    CarouselReelComponent,
    PersonBadgeComponent,
    TranslocoDirective,
    ImageComponent,
    BadgeExpanderComponent,
    SafeUrlPipe,
  ],
  templateUrl: './details-tab.component.html',
  styleUrl: './details-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DetailsTabComponent {

  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly accountService = inject(AccountService);

  protected readonly PersonRole = PersonRole;
  protected readonly FilterField = FilterField;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) metadata!: IHasCast;
  @Input() genres: Array<Genre> = [];
  @Input() tags: Array<Tag> = [];
  @Input() webLinks: Array<string> = [];
  @Input() suppressEmptyGenres: boolean = false;
  @Input() suppressEmptyTags: boolean = false;
  @Input() filePaths: string[] | undefined;


  openGeneric(queryParamName: FilterField, filter: string | number) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }
}
