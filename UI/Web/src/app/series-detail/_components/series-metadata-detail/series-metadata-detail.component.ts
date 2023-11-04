import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  Input,
  OnChanges,
  SimpleChanges
} from '@angular/core';
import {Router} from '@angular/router';
import {ReaderService} from 'src/app/_services/reader.service';
import {TagBadgeComponent, TagBadgeCursor} from '../../../shared/tag-badge/tag-badge.component';
import {FilterUtilitiesService} from '../../../shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from '../../../shared/_services/utility.service';
import {MangaFormat} from '../../../_models/manga-format';
import {ReadingList} from '../../../_models/reading-list';
import {Series} from '../../../_models/series';
import {SeriesMetadata} from '../../../_models/metadata/series-metadata';
import {ImageService} from 'src/app/_services/image.service';
import {CommonModule} from "@angular/common";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {ExternalRatingComponent} from "../external-rating/external-rating.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {PersonBadgeComponent} from "../../../shared/person-badge/person-badge.component";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {SeriesInfoCardsComponent} from "../../../cards/series-info-cards/series-info-cards.component";
import {LibraryType} from "../../../_models/library";
import {MetadataDetailComponent} from "../metadata-detail/metadata-detail.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";


@Component({
  selector: 'app-series-metadata-detail',
  standalone: true,
  imports: [CommonModule, TagBadgeComponent, BadgeExpanderComponent, SafeHtmlPipe, ExternalRatingComponent,
    ReadMoreComponent, A11yClickDirective, PersonBadgeComponent, NgbCollapse, SeriesInfoCardsComponent,
    MetadataDetailComponent, TranslocoDirective],
  templateUrl: './series-metadata-detail.component.html',
  styleUrls: ['./series-metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesMetadataDetailComponent implements OnChanges {

  @Input({required: true}) seriesMetadata!: SeriesMetadata;
  @Input({required: true}) libraryType!: LibraryType;
  @Input() hasReadingProgress: boolean = false;
  /**
   * Reading lists with a connection to the Series
   */
  @Input() readingLists: Array<ReadingList> = [];
  @Input({required: true}) series!: Series;

  isCollapsed: boolean = true;
  hasExtendedProperties: boolean = false;

  protected readonly imageService = inject(ImageService);
  protected readonly utilityService = inject(UtilityService);
  private readonly router = inject(Router);
  private readonly readerService = inject(ReaderService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);

  /**
   * Html representation of Series Summary
   */
  seriesSummary: string = '';

  protected FilterField = FilterField;
  protected LibraryType = LibraryType;
  protected MangaFormat = MangaFormat;
  protected TagBadgeCursor = TagBadgeCursor;

  get WebLinks() {
    if (this.seriesMetadata?.webLinks === '') return [];
    return this.seriesMetadata?.webLinks.split(',') || [];
  }

  constructor() {
    // If on desktop, we can just have all the data expanded by default:
    this.isCollapsed = this.utilityService.getActiveBreakpoint() < Breakpoint.Desktop;
    this.cdRef.markForCheck();
  }

  ngOnChanges(changes: SimpleChanges): void {
    this.hasExtendedProperties = this.seriesMetadata.colorists.length > 0 ||
                                  this.seriesMetadata.editors.length > 0 ||
                                  this.seriesMetadata.coverArtists.length > 0 ||
                                  this.seriesMetadata.inkers.length > 0 ||
                                  this.seriesMetadata.letterers.length > 0 ||
                                  this.seriesMetadata.pencillers.length > 0 ||
                                  this.seriesMetadata.publishers.length > 0 ||
                                  this.seriesMetadata.characters.length > 0 ||
                                  this.seriesMetadata.translators.length > 0;


    this.seriesSummary = (this.seriesMetadata?.summary === null ? '' : this.seriesMetadata.summary).replace(/\n/g, '<br>');
    this.cdRef.markForCheck();
  }

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
    this.cdRef.markForCheck();
  }

  handleGoTo(event: {queryParamName: FilterField, filter: any}) {
    this.goTo(event.queryParamName, event.filter);
  }

  goTo(queryParamName: FilterField, filter: any) {
    this.filterUtilityService.applyFilter(['library', this.series.libraryId], queryParamName,
      FilterComparison.Equal, filter).subscribe();
  }

  navigate(basePage: string, id: number) {
    this.router.navigate([basePage, id]);
  }
}
