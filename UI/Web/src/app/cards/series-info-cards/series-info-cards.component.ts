import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output } from '@angular/core';
import { debounceTime, filter, map, Subject, takeUntil } from 'rxjs';
import { FilterQueryParam } from 'src/app/shared/_services/filter-utilities.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { UserProgressUpdateEvent } from 'src/app/_models/events/user-progress-update-event';
import { HourEstimateRange } from 'src/app/_models/series-detail/hour-estimate-range';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/metadata/series-metadata';
import { AccountService } from 'src/app/_services/account.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { ReaderService } from 'src/app/_services/reader.service';
import {FilterField} from "../../_models/metadata/v2/filter-field";

@Component({
  selector: 'app-series-info-cards',
  templateUrl: './series-info-cards.component.html',
  styleUrls: ['./series-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesInfoCardsComponent implements OnInit, OnChanges, OnDestroy {

  @Input() series!: Series;
  @Input() seriesMetadata!: SeriesMetadata;
  @Input() hasReadingProgress: boolean = false;
  @Input() readingTimeLeft: HourEstimateRange | undefined;
  /**
   * If this should make an API call to request readingTimeLeft
   */
  @Input() showReadingTimeLeft: boolean = true;
  @Output() goTo: EventEmitter<{queryParamName: FilterField, filter: any}> = new EventEmitter();

  readingTime: HourEstimateRange = {avgHours: 0, maxHours: 0, minHours: 0};

  private readonly onDestroy = new Subject<void>();

  get MangaFormat() {
    return MangaFormat;
  }

  get FilterField() {
    return FilterField;
  }

  constructor(public utilityService: UtilityService, public metadataService: MetadataService,
    private readerService: ReaderService, private readonly cdRef: ChangeDetectorRef,
    private messageHub: MessageHubService, private accountService: AccountService) {
      // Listen for progress events and re-calculate getTimeLeft
      this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
                                    map(evt => evt.payload as UserProgressUpdateEvent),
                                    debounceTime(500),
                                    takeUntil(this.onDestroy))
        .subscribe(updateEvent => {
          this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
            if (user === undefined || user.username !== updateEvent.username) return;
            if (updateEvent.seriesId !== this.series.id) return;
            this.getReadingTimeLeft();
          });
        });
  }

  ngOnInit(): void {
    if (this.series !== null) {
      this.getReadingTimeLeft();
      this.readingTime.minHours = this.series.minHoursToRead;
      this.readingTime.maxHours = this.series.maxHoursToRead;
      this.readingTime.avgHours = this.series.avgHoursToRead;
      this.cdRef.markForCheck();
    }
  }

  ngOnChanges() {
    this.cdRef.markForCheck();
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  handleGoTo(queryParamName: FilterField, filter: any) {
    this.goTo.emit({queryParamName, filter});
  }

  private getReadingTimeLeft() {
    if (this.showReadingTimeLeft) this.readerService.getTimeLeft(this.series.id).subscribe((timeLeft) => {
      this.readingTimeLeft = timeLeft;
      this.cdRef.markForCheck();
    });
  }
}
