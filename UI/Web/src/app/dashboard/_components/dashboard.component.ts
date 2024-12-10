import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {Router, RouterLink} from '@angular/router';
import {Observable, ReplaySubject, Subject, switchMap} from 'rxjs';
import {debounceTime, map, shareReplay, take, tap, throttleTime} from 'rxjs/operators';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Library} from 'src/app/_models/library/library';
import {RecentlyAddedItem} from 'src/app/_models/recently-added-item';
import {SortField} from 'src/app/_models/metadata/series-filter';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CardItemComponent} from '../../cards/card-item/card-item.component';
import {SeriesCardComponent} from '../../cards/series-card/series-card.component';
import {CarouselReelComponent} from '../../carousel/_components/carousel-reel/carousel-reel.component';
import {AsyncPipe, NgTemplateOutlet} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {DashboardService} from "../../_services/dashboard.service";
import {MetadataService} from "../../_services/metadata.service";
import {RecommendationService} from "../../_services/recommendation.service";
import {Genre} from "../../_models/metadata/genre";
import {DashboardStream} from "../../_models/dashboard/dashboard-stream";
import {StreamType} from "../../_models/dashboard/stream-type.enum";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {ReaderService} from "../../_services/reader.service";
import {QueryContext} from "../../_models/metadata/v2/query-context";

enum StreamId {
  OnDeck,
  RecentlyUpdatedSeries,
  NewlyAddedSeries,
  MoreInGenre,
}


@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, RouterLink, CarouselReelComponent, SeriesCardComponent,
    CardItemComponent, AsyncPipe, TranslocoDirective, NgTemplateOutlet, LoadingComponent],
})
export class DashboardComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);
  private readonly recommendationService = inject(RecommendationService);
  protected readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);
  private readonly seriesService = inject(SeriesService);
  private readonly router = inject(Router);
  private readonly titleService = inject(Title);
  public readonly imageService = inject(ImageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly dashboardService = inject(DashboardService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly toastr = inject(ToastrService);
  private readonly readerService = inject(ReaderService);

  libraries$: Observable<Library[]> = this.libraryService.getLibraries().pipe(take(1), takeUntilDestroyed(this.destroyRef))
  isLoadingDashboard = true;

  streams: Array<DashboardStream> = [];
  genre: Genre | undefined;
  refreshStreams$ = new Subject<void>();
  refreshStreamsFromDashboardUpdate$ = new Subject<void>();

  streamCount: number = 0;
  streamsLoaded: number = 0;

  /**
   * We use this Replay subject to slow the amount of times we reload the UI
   */
  private loadRecentlyAdded$: ReplaySubject<void> = new ReplaySubject<void>();
  protected readonly StreamType = StreamType;
  protected readonly StreamId = StreamId;

  constructor() {
    this.loadDashboard();

    this.refreshStreamsFromDashboardUpdate$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(1000),
      tap(() => {
        this.loadDashboard();
      }))
      .subscribe();

    this.refreshStreams$.pipe(takeUntilDestroyed(this.destroyRef), throttleTime(10_000),
        tap(() => {
          this.loadDashboard()
        }))
        .subscribe();


    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      // TODO: Make the event have a stream Id so I can refresh just that stream
      if (res.event === EVENTS.DashboardUpdate) {
        this.refreshStreamsFromDashboardUpdate$.next();
      } else if (res.event === EVENTS.SeriesAdded) {
        this.refreshStreams$.next();
      } else if (res.event === EVENTS.SeriesRemoved) {
        this.refreshStreams$.next();
      } else if (res.event === EVENTS.ScanSeries) {
        // We don't have events for when series are updated, but we do get events when a scan update occurs. Refresh recentlyAdded at that time.
        this.loadRecentlyAdded$.next();
        this.refreshStreams$.next();
      }
    });

    this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
      if (hasExpired) {
        this.toastr.error(translate('toasts.anilist-token-expired'));
      }
      this.cdRef.markForCheck();
    });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita');
  }


  loadDashboard() {
    this.isLoadingDashboard = true;
    this.streamsLoaded = 0;
    this.streamCount = 0;
    this.cdRef.markForCheck();
    this.dashboardService.getDashboardStreams().subscribe(streams => {
      this.streams = streams;
      this.streamCount = streams.length;
      this.streams.forEach(s => {
        switch (s.streamType) {
          case StreamType.OnDeck:
            s.api = this.seriesService.getOnDeck(0, 1, 20)
                .pipe(map(d => d.result), tap(() => this.increment()), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.NewlyAdded:
            s.api = this.seriesService.getRecentlyAdded(1, 20)
                .pipe(map(d => d.result), tap(() => this.increment()), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.RecentlyUpdated:
            s.api = this.seriesService.getRecentlyUpdatedSeries().pipe(tap(() => this.increment()), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.SmartFilter:
            s.api = this.filterUtilityService.decodeFilter(s.smartFilterEncoded!).pipe(
              switchMap(filter => {
                return this.seriesService.getAllSeriesV2(0, 20, filter, QueryContext.Dashboard);
              }))
                .pipe(map(d => d.result),tap(() => this.increment()), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.MoreInGenre:
            s.api = this.metadataService.getAllGenres([], QueryContext.Dashboard).pipe(
                map(genres => {
                  this.genre = genres[Math.floor(Math.random() * genres.length)];
                  return this.genre;
                }),
                switchMap(genre => this.recommendationService.getMoreIn(0, genre.id, 0, 30)),
                map(p => p.result),
                tap(() => this.increment()),
                takeUntilDestroyed(this.destroyRef),
                shareReplay({bufferSize: 1, refCount: true})
            );
            break;
        }
      });
      this.isLoadingDashboard = false;
      this.cdRef.markForCheck();
    });
  }

  increment() {
    this.streamsLoaded++;
    this.cdRef.markForCheck();
  }

  reloadStream(streamId: number, onDeck = false) {
    const index = this.streams.findIndex(s => s.id === streamId);
    if (index < 0) return;
    if (onDeck) {
      // TODO: Need to figure out a better way to refresh just one stream
      this.refreshStreams$.next();
      this.cdRef.markForCheck();
    } else {
      this.streams[index] = {...this.streams[index]};
      this.cdRef.markForCheck();
    }
  }

  async handleRecentlyAddedChapterClick(item: RecentlyAddedItem) {
    await this.router.navigate(['library', item.libraryId, 'series', item.seriesId]);
  }

  async handleRecentlyAddedChapterRead(item: RecentlyAddedItem) {
    // Get Continue Reading point and open directly
    this.readerService.getCurrentChapter(item.seriesId).subscribe(chapter => {
      this.readerService.readChapter(item.libraryId, item.seriesId, chapter, false);
    });
  }

  async handleFilterSectionClick(stream: DashboardStream) {
    await this.router.navigateByUrl('all-series?' + stream.smartFilterEncoded);
  }

  handleSectionClick(streamId: StreamId) {
    if (streamId === StreamId.RecentlyUpdatedSeries) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.recently-updated-title');
      const filter = this.filterUtilityService.createSeriesV2Filter();
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.LastChapterAdded;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.OnDeck) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.on-deck-title');

      const filter = this.filterUtilityService.createSeriesV2Filter();
      filter.statements.push({field: FilterField.ReadProgress, comparison: FilterComparison.GreaterThan, value: '0'});
      filter.statements.push({field: FilterField.ReadProgress, comparison: FilterComparison.NotEqual, value: '100'});
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.LastChapterAdded;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.NewlyAddedSeries) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.recently-added-title');
      const filter = this.filterUtilityService.createSeriesV2Filter();
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.Created;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.MoreInGenre) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.more-in-genre-title', {genre: this.genre?.title});
      const filter = this.filterUtilityService.createSeriesV2Filter();
      filter.statements.push({field: FilterField.Genres, value: this.genre?.id + '', comparison: FilterComparison.MustContains});
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    }
  }

  protected readonly SettingsTabId = SettingsTabId;
}
