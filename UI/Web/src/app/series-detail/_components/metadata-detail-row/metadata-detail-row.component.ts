import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {AgeRatingImageComponent} from "../../../_single-module/age-rating-image/age-rating-image.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {ReadTimeLeftPipe} from "../../../_pipes/read-time-left.pipe";
import {ReadTimePipe} from "../../../_pipes/read-time.pipe";
import {IHasCast} from "../../../_models/common/i-has-cast";
import {HourEstimateRange} from "../../../_models/series-detail/hour-estimate-range";
import {AgeRating} from "../../../_models/metadata/age-rating";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {IHasReadingTime} from "../../../_models/common/i-has-reading-time";
import {TranslocoDirective} from "@jsverse/transloco";
import {LibraryType} from "../../../_models/library/library";
import {ImageComponent} from "../../../shared/image/image.component";
import {ImageService} from "../../../_services/image.service";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {MangaFormat} from "../../../_models/manga-format";
import {SeriesFormatComponent} from "../../../shared/series-format/series-format.component";
import {PublisherFlipperComponent} from "../../../_single-module/publisher-flipper/publisher-flipper.component";

@Component({
  selector: 'app-metadata-detail-row',
  standalone: true,
  imports: [
    AgeRatingImageComponent,
    CompactNumberPipe,
    ReadTimeLeftPipe,
    ReadTimePipe,
    NgbTooltip,
    TranslocoDirective,
    ImageComponent,
    SeriesFormatComponent,
    PublisherFlipperComponent
  ],
  templateUrl: './metadata-detail-row.component.html',
  styleUrl: './metadata-detail-row.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataDetailRowComponent {
  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);

  protected readonly LibraryType = LibraryType;
  protected readonly FilterField = FilterField;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) entity!: IHasCast;
  @Input({required: true}) readingTimeEntity!: IHasReadingTime;
  @Input({required: true}) hasReadingProgress: boolean = false;
  @Input() readingTimeLeft: HourEstimateRange | null = null;
  @Input({required: true}) ageRating: AgeRating = AgeRating.Unknown;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) mangaFormat!: MangaFormat;

  openGeneric(queryParamName: FilterField, filter: string | number) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }
}
