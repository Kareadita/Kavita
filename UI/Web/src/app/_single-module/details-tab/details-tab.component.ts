import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasCast} from "../../_models/common/i-has-cast";
import {PersonRole} from "../../_models/metadata/person";
import {SeriesFilterField} from "../../_models/metadata/v2/series-filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {Genre} from "../../_models/metadata/genre";
import {Tag} from "../../_models/tag";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {MangaFormat} from "../../_models/manga-format";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";
import {AccountService} from "../../_services/account.service";
import {MangaFile} from "../../_models/manga-file";
import {Series} from "../../_models/series";
import {Volume} from "../../_models/volume";
import {Chapter} from "../../_models/chapter";
import {
  ExternalMetadataDetailComponent
} from "../../shared/_components/external-metadata-detail/external-metadata-detail.component";

@Component({
  selector: 'app-details-tab',
  imports: [
    CarouselReelComponent,
    PersonBadgeComponent,
    TranslocoDirective,
    ImageComponent,
    BadgeExpanderComponent,
    SafeUrlPipe,
    ExternalMetadataDetailComponent,

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
  protected readonly FilterField = SeriesFilterField;
  protected readonly MangaFormat = MangaFormat;

  metadata = input.required<IHasCast>();
  entity = input<Series | Volume | Chapter>();
  genres = input<Genre[]>([]);
  tags = input<Tag[]>([]);
  webLinks = input<string[]>([]);
  suppressEmptyGenres = input<boolean>(false);
  suppressEmptyTags = input<boolean>(false);
  filePaths = input<string[]>([]);
  files = input<MangaFile[]>([]);

  hasUpperMetadata = computed(() => {
    return this.genres().length > 0 || this.tags().length > 0 || this.webLinks().length > 0;
  });

  showTags = computed(() => !this.suppressEmptyTags() || this.tags().length > 0);
  showGenres = computed(() => !this.suppressEmptyGenres() || this.genres().length > 0);


  openGeneric(queryParamName: SeriesFilterField, filter: string | number) {
    if (queryParamName === SeriesFilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }
}
