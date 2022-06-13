import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FilterQueryParam } from 'src/app/shared/_services/filter-utilities.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { HourEstimateRange } from 'src/app/_models/hour-estimate-range';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/series-metadata';
import { MetadataService } from 'src/app/_services/metadata.service';
import { ReaderService } from 'src/app/_services/reader.service';

@Component({
  selector: 'app-series-info-cards',
  templateUrl: './series-info-cards.component.html',
  styleUrls: ['./series-info-cards.component.scss']
})
export class SeriesInfoCardsComponent implements OnInit {

  @Input() series!: Series;
  @Input() seriesMetadata!: SeriesMetadata;
  @Input() hasReadingProgress: boolean = false;
  @Input() readingTimeLeft: HourEstimateRange | undefined;
  /**
   * If this should make an API call to request readingTimeLeft
   */
  @Input() showReadingTimeLeft: boolean = true;
  @Output() goTo: EventEmitter<{queryParamName: FilterQueryParam, filter: any}> = new EventEmitter();

  readingTime: HourEstimateRange = {avgHours: 0, maxHours: 0, minHours: 0};

  get MangaFormat() {
    return MangaFormat;
  }

  get FilterQueryParam() {
    return FilterQueryParam;
  }

  constructor(public utilityService: UtilityService, public metadataService: MetadataService, private readerService: ReaderService) { }

  ngOnInit(): void {
    if (this.series !== null) {
      if (this.showReadingTimeLeft) this.readerService.getTimeLeft(this.series.id).subscribe((timeLeft) => this.readingTimeLeft = timeLeft);
      this.readingTime.minHours = this.series.minHoursToRead;
      this.readingTime.maxHours = this.series.maxHoursToRead;
      this.readingTime.avgHours = this.series.avgHoursToRead;
    }
  }

  handleGoTo(queryParamName: FilterQueryParam, filter: any) {
    this.goTo.emit({queryParamName, filter});
  }

 

}
