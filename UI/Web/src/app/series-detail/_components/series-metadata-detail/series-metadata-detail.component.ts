import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ReaderService } from 'src/app/_services/reader.service';
import {TagBadgeComponent, TagBadgeCursor} from '../../../shared/tag-badge/tag-badge.component';
import { FilterQueryParam } from '../../../shared/_services/filter-utilities.service';
import { UtilityService } from '../../../shared/_services/utility.service';
import { MangaFormat } from '../../../_models/manga-format';
import { ReadingList } from '../../../_models/reading-list';
import { Series } from '../../../_models/series';
import { SeriesMetadata } from '../../../_models/metadata/series-metadata';
import { ImageService } from 'src/app/_services/image.service';
import {CommonModule} from "@angular/common";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {ExternalRatingComponent} from "../external-rating/external-rating.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {PersonBadgeComponent} from "../../../shared/person-badge/person-badge.component";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {SeriesInfoCardsComponent} from "../../../cards/series-info-cards/series-info-cards.component";
import {LibraryType} from "../../../_models/library";
import {MetadataDetailComponent} from "../metadata-detail/metadata-detail.component";
import {TranslocoModule} from "@ngneat/transloco";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {MetadataService} from "../../../_services/metadata.service";



@Component({
  selector: 'app-series-metadata-detail',
  standalone: true,
  imports: [CommonModule, TagBadgeComponent, BadgeExpanderComponent, SafeHtmlPipe, ExternalRatingComponent,
    ReadMoreComponent, A11yClickDirective, PersonBadgeComponent, NgbCollapse, SeriesInfoCardsComponent,
    MetadataDetailComponent, TranslocoModule],
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

  imageService = inject(ImageService);

  /**
   * Html representation of Series Summary
   */
  seriesSummary: string = '';

  get LibraryType() { return LibraryType; }
  get MangaFormat() { return MangaFormat; }
  get TagBadgeCursor() { return TagBadgeCursor; }
  get FilterQueryParam() { return FilterQueryParam; }

  get FilterField() {
    return FilterField;
  }

  get WebLinks() {
    if (this.seriesMetadata?.webLinks === '') return [];
    return this.seriesMetadata?.webLinks.split(',') || [];
  }

  constructor(public utilityService: UtilityService,
    private router: Router, public readerService: ReaderService,
    private readonly cdRef: ChangeDetectorRef, private metadataService: MetadataService) {

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
    this.metadataService.applyFilter(['library', this.series.libraryId], queryParamName, FilterComparison.Equal, filter);
  }

  navigate(basePage: string, id: number) {
    this.router.navigate([basePage, id]);
  }
}
