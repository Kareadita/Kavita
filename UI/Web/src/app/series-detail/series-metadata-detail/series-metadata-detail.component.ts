import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { Router } from '@angular/router';
import { TagBadgeCursor } from '../../shared/tag-badge/tag-badge.component';
import { FilterQueryParam } from '../../shared/_services/filter-utilities.service';
import { UtilityService } from '../../shared/_services/utility.service';
import { MangaFormat } from '../../_models/manga-format';
import { ReadingList } from '../../_models/reading-list';
import { Series } from '../../_models/series';
import { SeriesMetadata } from '../../_models/series-metadata';
import { MetadataService } from '../../_services/metadata.service';

const MAX_WORDS_PER_HOUR = 30_000;
const MIN_WORDS_PER_HOUR = 10_260;
const MAX_PAGES_PER_MINUTE = 2.75;
const MIN_PAGES_PER_MINUTE = 3.33;


@Component({
  selector: 'app-series-metadata-detail',
  templateUrl: './series-metadata-detail.component.html',
  styleUrls: ['./series-metadata-detail.component.scss']
})
export class SeriesMetadataDetailComponent implements OnInit, OnChanges {

  @Input() seriesMetadata!: SeriesMetadata;
  /**
   * Reading lists with a connection to the Series
   */
  @Input() readingLists: Array<ReadingList> = [];
  @Input() series!: Series;

  isCollapsed: boolean = true;
  hasExtendedProperites: boolean = false;

  minHoursToRead: number = 1;
  maxHoursToRead: number = 1;

  /**
   * Html representation of Series Summary
   */
  seriesSummary: string = '';

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  get TagBadgeCursor(): typeof TagBadgeCursor {
    return TagBadgeCursor;
  }

  get FilterQueryParam() {
    return FilterQueryParam;
  }

  constructor(public utilityService: UtilityService, public metadataService: MetadataService, private router: Router) { }
  
  ngOnChanges(changes: SimpleChanges): void {
    this.hasExtendedProperites = this.seriesMetadata.colorists.length > 0 || 
                                  this.seriesMetadata.editors.length > 0 || 
                                  this.seriesMetadata.coverArtists.length > 0 || 
                                  this.seriesMetadata.inkers.length > 0 ||
                                  this.seriesMetadata.letterers.length > 0 ||
                                  this.seriesMetadata.pencillers.length > 0 ||
                                  this.seriesMetadata.publishers.length > 0 || 
                                  this.seriesMetadata.translators.length > 0 ||
                                  this.seriesMetadata.tags.length > 0;

    if (this.seriesMetadata !== null) {
      this.seriesSummary = (this.seriesMetadata.summary === null ? '' : this.seriesMetadata.summary).replace(/\n/g, '<br>');

      
      
    }
    if (this.series !== null) {
      if (this.series.format === MangaFormat.EPUB && this.series.wordCount > 0) {
        this.minHoursToRead = parseInt(Math.round(this.series.wordCount / MAX_WORDS_PER_HOUR) + '', 10);
        this.maxHoursToRead = parseInt(Math.round(this.series.wordCount / MIN_WORDS_PER_HOUR) + '', 10);
      } else if (this.series.format !== MangaFormat.EPUB) {
        this.minHoursToRead = parseInt(Math.round((this.series.pages / MIN_PAGES_PER_MINUTE) / 60) + '', 10);
        this.maxHoursToRead = parseInt(Math.round((this.series.pages / MAX_PAGES_PER_MINUTE) / 60) + '', 10);
      }
    }
  }

  ngOnInit(): void {
  }

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
  }

  goTo(queryParamName: FilterQueryParam, filter: any) {
    let params: any = {};
    params[queryParamName] = filter;
    params[FilterQueryParam.Page] = 1;
    this.router.navigate(['library', this.series.libraryId], {queryParams: params});
  }

}
