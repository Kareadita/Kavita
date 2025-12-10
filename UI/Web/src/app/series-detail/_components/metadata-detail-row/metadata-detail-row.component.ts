import {ChangeDetectionStrategy, Component, computed, inject, input, Input} from '@angular/core';
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
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-metadata-detail-row',
  imports: [
    AgeRatingImageComponent,
    CompactNumberPipe,
    ReadTimeLeftPipe,
    ReadTimePipe,
    NgbTooltip,
    TranslocoDirective,
    ImageComponent,
    SeriesFormatComponent,
    BytesPipe
  ],
  standalone: true,
  templateUrl: './metadata-detail-row.component.html',
  styleUrl: './metadata-detail-row.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataDetailRowComponent {
  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly accountService = inject(AccountService);

  entity = input.required<IHasCast>();
  readingTimeEntity = input.required<IHasReadingTime>();
  hasReadingProgress = input<boolean>(false);
  readingTimeLeft = input<HourEstimateRange | null>(null);
  ageRating = input<AgeRating>(AgeRating.Unknown);
  libraryType = input.required<LibraryType>();
  mangaFormat = input.required<MangaFormat>();
  releaseYear = input<number | undefined>(undefined);
  totalBytes = input<number | undefined>(undefined);
  totalReads = input(0);

  hasDownloadRole = computed(() => {
    const user = this.accountService.currentUserSignal();
    return user && this.accountService.hasDownloadRole(user);
  });

  openGeneric(queryParamName: FilterField, filter: string | number) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }

  protected readonly LibraryType = LibraryType;
  protected readonly FilterField = FilterField;
  protected readonly MangaFormat = MangaFormat;
}
