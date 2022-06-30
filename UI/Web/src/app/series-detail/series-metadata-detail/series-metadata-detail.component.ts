import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { Router } from '@angular/router';
import { HourEstimateRange } from 'src/app/_models/hour-estimate-range';
import { ReaderService } from 'src/app/_services/reader.service';
import { TagBadgeCursor } from '../../shared/tag-badge/tag-badge.component';
import { FilterQueryParam } from '../../shared/_services/filter-utilities.service';
import { UtilityService } from '../../shared/_services/utility.service';
import { MangaFormat } from '../../_models/manga-format';
import { ReadingList } from '../../_models/reading-list';
import { Series } from '../../_models/series';
import { SeriesMetadata } from '../../_models/series-metadata';
import { MetadataService } from '../../_services/metadata.service';


@Component({
  selector: 'app-series-metadata-detail',
  templateUrl: './series-metadata-detail.component.html',
  styleUrls: ['./series-metadata-detail.component.scss']
})
export class SeriesMetadataDetailComponent implements OnInit, OnChanges {

  @Input() seriesMetadata!: SeriesMetadata;
  @Input() hasReadingProgress: boolean = false;
  /**
   * Reading lists with a connection to the Series
   */
  @Input() readingLists: Array<ReadingList> = [];
  @Input() series!: Series;

  isCollapsed: boolean = true;
  hasExtendedProperites: boolean = false;

  // readingTime: HourEstimateRange = {maxHours: 1, minHours: 1, avgHours: 1};
  // readingTimeLeft: HourEstimateRange = {maxHours: 1, minHours: 1, avgHours: 1};

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

  constructor(public utilityService: UtilityService, public metadataService: MetadataService, private router: Router, public readerService: ReaderService) {
    
  }
  
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
  }

  ngOnInit(): void {
  }

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
  }

  handleGoTo(event: {queryParamName: FilterQueryParam, filter: any}) {
    this.goTo(event.queryParamName, event.filter);
  }

  goTo(queryParamName: FilterQueryParam, filter: any) {
    let params: any = {};
    params[queryParamName] = filter;
    params[FilterQueryParam.Page] = 1;
    this.router.navigate(['library', this.series.libraryId], {queryParams: params});
  }

}
