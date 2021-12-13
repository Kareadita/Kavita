import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { TagBadgeCursor } from '../shared/tag-badge/tag-badge.component';
import { UtilityService } from '../shared/_services/utility.service';
import { MangaFormat } from '../_models/manga-format';
import { Series } from '../_models/series';
import { SeriesMetadata } from '../_models/series-metadata';
import { MetadataService } from '../_services/metadata.service';

@Component({
  selector: 'app-series-metadata-detail',
  templateUrl: './series-metadata-detail.component.html',
  styleUrls: ['./series-metadata-detail.component.scss']
})
export class SeriesMetadataDetailComponent implements OnInit, OnChanges {

  @Input() seriesMetadata!: SeriesMetadata;
  @Input() series!: Series;

  isCollapsed: boolean = true;
  hasExtendedProperites: boolean = false;

  /**
   * String representation of AgeRating enum
   */
  ageRatingName: string = '';
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

  constructor(public utilityService: UtilityService, private metadataService: MetadataService) { }
  
  ngOnChanges(changes: SimpleChanges): void {
    this.hasExtendedProperites = this.seriesMetadata.colorists.length > 0 || 
                                  this.seriesMetadata.editors.length > 0 || 
                                  this.seriesMetadata.artists.length > 0 || 
                                  this.seriesMetadata.inkers.length > 0 ||
                                  this.seriesMetadata.letterers.length > 0 ||
                                  this.seriesMetadata.pencillers.length > 0 ||
                                  this.seriesMetadata.publishers.length > 0;

    this.metadataService.getAgeRating(this.seriesMetadata.ageRating).subscribe(rating => {
      this.ageRatingName = rating;
    });

    if (this.seriesMetadata !== null) {
      this.seriesSummary = (this.seriesMetadata.summary === null ? '' : this.seriesMetadata.summary).replace(/\n/g, '<br>');
    }
    
  }

  ngOnInit(): void {
  }

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
  }


}
