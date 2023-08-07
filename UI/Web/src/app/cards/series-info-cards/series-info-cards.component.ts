import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  OnInit,
  Output
} from '@angular/core';
import {debounceTime, filter, map} from 'rxjs';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { UserProgressUpdateEvent } from 'src/app/_models/events/user-progress-update-event';
import { HourEstimateRange } from 'src/app/_models/series-detail/hour-estimate-range';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { SeriesMetadata } from 'src/app/_models/metadata/series-metadata';
import { AccountService } from 'src/app/_services/account.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { ReaderService } from 'src/app/_services/reader.service';
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {CommonModule} from "@angular/common";
import {IconAndTitleComponent} from "../../shared/icon-and-title/icon-and-title.component";
import {AgeRatingPipe} from "../../pipe/age-rating.pipe";
import {DefaultValuePipe} from "../../pipe/default-value.pipe";
import {LanguageNamePipe} from "../../pipe/language-name.pipe";
import {PublicationStatusPipe} from "../../pipe/publication-status.pipe";
import {MangaFormatPipe} from "../../pipe/manga-format.pipe";
import {TimeAgoPipe} from "../../pipe/time-ago.pipe";
import {CompactNumberPipe} from "../../pipe/compact-number.pipe";
import {MangaFormatIconPipe} from "../../pipe/manga-format-icon.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoModule} from "@ngneat/transloco";

@Component({
  selector: 'app-series-info-cards',
  standalone: true,
  imports: [CommonModule, IconAndTitleComponent, AgeRatingPipe, DefaultValuePipe, LanguageNamePipe, PublicationStatusPipe, MangaFormatPipe, TimeAgoPipe, CompactNumberPipe, MangaFormatIconPipe, NgbTooltip, TranslocoModule],
  templateUrl: './series-info-cards.component.html',
  styleUrls: ['./series-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesInfoCardsComponent implements OnInit, OnChanges {

  @Input({required: true}) series!: Series;
  @Input({required: true}) seriesMetadata!: SeriesMetadata;
  @Input() hasReadingProgress: boolean = false;
  @Input() readingTimeLeft: HourEstimateRange | undefined;
  /**
   * If this should make an API call to request readingTimeLeft
   */
  @Input() showReadingTimeLeft: boolean = true;
  @Output() goTo: EventEmitter<{queryParamName: FilterField, filter: any}> = new EventEmitter();

  readingTime: HourEstimateRange = {avgHours: 0, maxHours: 0, minHours: 0};
  isScrobbling: boolean = true;
  libraryAllowsScrobbling: boolean = true;
  private readonly destroyRef = inject(DestroyRef);

  get MangaFormat() {
    return MangaFormat;
  }

  get FilterField() {
    return FilterField;
  }

  constructor(public utilityService: UtilityService, private readerService: ReaderService,
              private readonly cdRef: ChangeDetectorRef, private messageHub: MessageHubService,
              public accountService: AccountService, private scrobbleService: ScrobblingService) {
      // Listen for progress events and re-calculate getTimeLeft
      this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
                                    map(evt => evt.payload as UserProgressUpdateEvent),
                                    debounceTime(500),
                                    takeUntilDestroyed(this.destroyRef))
        .subscribe(updateEvent => {
          this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
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
      this.scrobbleService.hasHold(this.series.id).subscribe(res => {
        this.isScrobbling = !res;
        this.cdRef.markForCheck();
      });

      this.scrobbleService.libraryAllowsScrobbling(this.series.id).subscribe(res => {
        this.libraryAllowsScrobbling = res;
        this.cdRef.markForCheck();
      });

      this.cdRef.markForCheck();
    }
  }

  ngOnChanges() {
    this.cdRef.markForCheck();
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

  toggleScrobbling(evt: any) {
    evt.stopPropagation();
    if (this.isScrobbling) {
      this.scrobbleService.addHold(this.series.id).subscribe(() => {
        this.isScrobbling = !this.isScrobbling;
        this.cdRef.markForCheck();
      });
    } else {
      this.scrobbleService.removeHold(this.series.id).subscribe(() => {
        this.isScrobbling = !this.isScrobbling;
        this.cdRef.markForCheck();
      });
    }
  }
}
