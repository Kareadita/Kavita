import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  OnInit,
  Output
} from '@angular/core';
import {debounceTime, filter, map} from 'rxjs';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {UserProgressUpdateEvent} from 'src/app/_models/events/user-progress-update-event';
import {HourEstimateRange} from 'src/app/_models/series-detail/hour-estimate-range';
import {MangaFormat} from 'src/app/_models/manga-format';
import {Series} from 'src/app/_models/series';
import {SeriesMetadata} from 'src/app/_models/metadata/series-metadata';
import {AccountService} from 'src/app/_services/account.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {ReaderService} from 'src/app/_services/reader.service';
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {CommonModule} from "@angular/common";
import {IconAndTitleComponent} from "../../shared/icon-and-title/icon-and-title.component";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LanguageNamePipe} from "../../_pipes/language-name.pipe";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {TimeAgoPipe} from "../../_pipes/time-ago.pipe";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {MangaFormatIconPipe} from "../../_pipes/manga-format-icon.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-series-info-cards',
  standalone: true,
  imports: [CommonModule, IconAndTitleComponent, AgeRatingPipe, DefaultValuePipe, LanguageNamePipe, PublicationStatusPipe, MangaFormatPipe, TimeAgoPipe, CompactNumberPipe, MangaFormatIconPipe, NgbTooltip, TranslocoDirective],
  templateUrl: './series-info-cards.component.html',
  styleUrls: ['./series-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesInfoCardsComponent implements OnInit, OnChanges {

  private readonly destroyRef = inject(DestroyRef);
  public readonly utilityService = inject(UtilityService);
  private readonly readerService = inject(ReaderService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly messageHub = inject(MessageHubService);
  public readonly accountService = inject(AccountService);
  private readonly scrobbleService = inject(ScrobblingService);

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


  protected readonly MangaFormat = MangaFormat;
  protected readonly FilterField = FilterField;


  constructor() {
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
    // Ignore the default case added as this query combo would never be valid
    if (filter + '' === '' && queryParamName === FilterField.SeriesName) return;
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
