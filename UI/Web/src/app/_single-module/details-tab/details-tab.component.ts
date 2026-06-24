import {ChangeDetectionStrategy, Component, computed, effect, inject, input, signal} from '@angular/core';
import {CarouselReelComponent} from '../../carousel/_components/carousel-reel/carousel-reel.component';
import {PersonBadgeComponent} from '../../shared/person-badge/person-badge.component';
import {TranslocoDirective} from '@jsverse/transloco';
import {IHasCast} from '../../_models/common/i-has-cast';
import {PersonRole} from '../../_models/metadata/person';
import {SeriesFilterField} from '../../_models/metadata/v2/series-filter-field';
import {FilterComparison} from '../../_models/metadata/v2/filter-comparison';
import {FilterUtilitiesService} from '../../shared/_services/filter-utilities.service';
import {Genre} from '../../_models/metadata/genre';
import {BaseTag} from '../../_models/tag';
import {ImageComponent} from '../../shared/image/image.component';
import {ImageService} from '../../_services/image.service';
import {MangaFormat} from '../../_models/manga-format';
import {SafeUrlPipe} from '../../_pipes/safe-url.pipe';
import {AccountService} from '../../_services/account.service';
import {MangaFile} from '../../_models/manga-file';
import {Series} from '../../_models/series';
import {Volume} from '../../_models/volume';
import {Chapter} from '../../_models/chapter';
import {
  ExternalMetadataDetailComponent
} from '../../shared/_components/external-metadata-detail/external-metadata-detail.component';
import {LabelCardComponent} from '../label-card/label-card.component';
import {TagBadgeComponent, TagBadgeCursor} from '../../shared/tag-badge/tag-badge.component';
import {DefaultValuePipe} from '../../_pipes/default-value.pipe';
import {BytesPipe} from '../../_pipes/bytes.pipe';
import {TimeAgoPipe} from '../../_pipes/time-ago.pipe';
import {DatePipe} from '@angular/common';
import {PublicationStatus} from '../../_models/metadata/publication-status';
import {PublicationStatusPipe} from '../../_pipes/publication-status.pipe';
import {ReadTimePipe} from '../../_pipes/read-time.pipe';
import {IHasReadingTime} from '../../_models/common/i-has-reading-time';
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {MetadataService} from "../../_services/metadata.service";

export interface BasicMetadataInfo {
  readingTime?: IHasReadingTime | null;
  pages?: number | null;
  words?: number | null;
  addedAt?: string | null;
  updatedAt?: string | null;
  kavitaId?: number | null;
  sortOrder?: number | null;
  isSpecial?: boolean | null;
  language?: string | null;
  publicationStatus?: PublicationStatus | null;
  publicationStatusCurrent?: number | null;
  publicationStatusTotal?: number | null;
}

@Component({
  selector: 'app-details-tab',
  imports: [
    CarouselReelComponent,
    PersonBadgeComponent,
    TranslocoDirective,
    ImageComponent,
    SafeUrlPipe,
    ExternalMetadataDetailComponent,
    LabelCardComponent,
    TagBadgeComponent,
    DefaultValuePipe,
    BytesPipe,
    TimeAgoPipe,
    DatePipe,
    PublicationStatusPipe,
    ReadTimePipe,
    CompactNumberPipe,
    NgbTooltip,
  ],
  templateUrl: './details-tab.component.html',
  styleUrl: './details-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DetailsTabComponent {

  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);
  protected readonly accountService = inject(AccountService);

  protected readonly PersonRole = PersonRole;
  protected readonly FilterField = SeriesFilterField;
  protected readonly MangaFormat = MangaFormat;
  protected readonly TagBadgeCursor = TagBadgeCursor;

  metadata = input.required<IHasCast>();
  entity = input<Series | Volume | Chapter>();
  genres = input<Genre[]>([]);
  tags = input<BaseTag[]>([]);
  webLinks = input<string[]>([]);
  suppressEmptyGenres = input<boolean>(false);
  suppressEmptyTags = input<boolean>(false);
  individualWork = input<boolean>(false);
  filePaths = input<string[]>([]);
  files = input<MangaFile[]>([]);
  basicMetadata = input<BasicMetadataInfo>();


  showBasicMetadata = computed(() => !!this.basicMetadata());
  hasUpperMetadata = computed(() => this.genres().length > 0 || this.tags().length > 0 || this.webLinks().length > 0);
  showTags = computed(() => !this.suppressEmptyTags() || this.tags().length > 0);
  showGenres = computed(() => !this.suppressEmptyGenres() || this.genres().length > 0);
  isbn = computed(() => {
    const entity = this.entity();
    if (!entity?.hasOwnProperty('isbn')) return null;

    return (this.entity() as Chapter).isbn;
  });
  languageName = signal<string | null>(null);
  languageDisplay = computed(() => {
    return this.languageName() ?? this.basicMetadata()?.language;
  });

  constructor() {
    effect(() => {
      const lang = this.basicMetadata()?.language;
      const langName = this.languageName();
      if (lang && !langName) {
        this.metadataService.getLanguageNameForCode(lang).subscribe(fullCode => {
          this.languageName.set(fullCode);
        });
      }
    });

  }

  openGeneric(queryParamName: SeriesFilterField, filter: string | number) {
    if (queryParamName === SeriesFilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter}`).subscribe();
  }
}
