import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {Router, RouterLink} from '@angular/router';
import {filter, Observable, ReplaySubject, Subject, switchMap} from 'rxjs';
import {debounceTime, map, shareReplay, take, tap, throttleTime} from 'rxjs/operators';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Library} from 'src/app/_models/library/library';
import {RecentlyAddedItem} from 'src/app/_models/recently-added-item';
import {SeriesSortField} from 'src/app/_models/metadata/series-filter';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CarouselReelComponent} from '../../carousel/_components/carousel-reel/carousel-reel.component';
import {AsyncPipe, NgTemplateOutlet} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesFilterField} from "../../_models/metadata/v2/series-filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {DashboardService} from "../../_services/dashboard.service";
import {MetadataService} from "../../_services/metadata.service";
import {RecommendationService} from "../../_services/recommendation.service";
import {Genre} from "../../_models/metadata/genre";
import {DashboardStream} from "../../_models/dashboard/dashboard-stream";
import {StreamType} from "../../_models/dashboard/stream-type.enum";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {ToastrService} from "ngx-toastr";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {ReaderService} from "../../_services/reader.service";
import {QueryContext} from "../../_models/metadata/v2/query-context";
import {LicenseService} from "../../_services/license.service";
import {EntityCardComponent} from "../../cards/entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {PromotedIconComponent} from "../../shared/_components/promoted-icon/promoted-icon.component";
import {FilterEntityType} from "../../_models/metadata/v2/filter-entity-type";
import {ReadingListService} from "../../_services/reading-list.service";
import {PersonService} from "../../_services/person.service";
import {AnnotationService} from "../../_services/annotation.service";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";

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
  imports: [SideNavCompanionBarComponent, RouterLink, CarouselReelComponent, AsyncPipe, TranslocoDirective, NgTemplateOutlet, LoadingComponent, EntityCardComponent, PromotedIconComponent]
})
export class DashboardComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);
  private readonly recommendationService = inject(RecommendationService);
  protected readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);
  protected readonly seriesService = inject(SeriesService);
  private readonly readingListService = inject(ReadingListService);
  private readonly personService = inject(PersonService);
  private readonly annotationService = inject(AnnotationService);
  private readonly router = inject(Router);
  public readonly imageService = inject(ImageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly dashboardService = inject(DashboardService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly toastr = inject(ToastrService);
  private readonly readerService = inject(ReaderService);
  private readonly licenseService = inject(LicenseService);
  private readonly cardConfigFactory = inject(CardConfigFactory);

  private readonly scrobbleProviderNamePipe = new ScrobbleProviderNamePipe();

  libraries$: Observable<Library[]> = this.libraryService.getLibraries().pipe(take(1), takeUntilDestroyed(this.destroyRef))
  isLoadingDashboard = signal<boolean>(true);

  streams: Array<DashboardStream> = [];
  genre: Genre | undefined;
  refreshStreams$ = new Subject<void>();
  refreshStreamsFromDashboardUpdate$ = new Subject<void>();

  streamCount: number = 0;
  streamsLoaded: number = 0;

  seriesConfig = computed(() => this.cardConfigFactory.forSeries(undefined, true));
  recentlyUpdatedConfig = computed(() => this.cardConfigFactory.forRecentlyUpdated({
    overrides: {
      readFunc: this.handleRecentlyAddedChapterRead.bind(this)
    }
  }));

  protected titleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');
  protected readonly readingListConfig = computed(() => this.cardConfigFactory.forReadingList({titleRef: this.titleTemplateRef(), overrides: {allowSelection: false, actionableFunc: () => []}}));


  /**
   * We use this Replay subject to slow the amount of times we reload the UI
   */
  private loadRecentlyAdded$: ReplaySubject<void> = new ReplaySubject<void>();
  protected readonly StreamType = StreamType;
  protected readonly StreamId = StreamId;

  constructor() {
    this.loadDashboard();

    this.refreshStreamsFromDashboardUpdate$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(1000),
      tap(() => this.loadDashboard()))
      .subscribe();

    this.refreshStreams$.pipe(takeUntilDestroyed(this.destroyRef), throttleTime(10_000),
        tap(() => this.loadDashboard()))
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

    if (this.licenseService.hasActiveLicense()) {
      this.scrobblingService.checkExpiredTokens()
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          filter(providers => providers.length > 0),
          map(providers => providers.map(this.scrobbleProviderNamePipe.transform).join(', ')),
          switchMap(providerNames => this.toastr.error(providerNames, translate('toasts.tokens-expired')).onTap),
          tap(() => this.router.navigateByUrl('/settings#' + SettingsTabId.ScrobbleSettings).catch(console.error))
        ).subscribe();
    }
  }

  smartFilterNextPage(stream: DashboardStream) {
    if (!stream.smartFilterDecoded) return null;

    return (pageNum: number, pageSize: number) => {
      return this.seriesService.getAllSeriesV2(pageNum, pageSize, stream.smartFilterDecoded, QueryContext.Dashboard)
        .pipe(map(d => d.result.map(series => CardEntityFactory.series(series))));
    }
  }

  onDeckNextPage(stream: DashboardStream) {
    return (pageNum: number, pageSize: number) => {
      return this.seriesService.getOnDeck(pageNum, pageSize)
        .pipe(map(d => d.result.map(series => CardEntityFactory.series(series))));
    }
  }
  onRecentlyAddedNextPage(stream: DashboardStream) {
    return (pageNum: number, pageSize: number) => {
      return this.seriesService.getRecentlyAdded(pageNum, pageSize)
        .pipe(map(d => d.result.map(series => CardEntityFactory.series(series))));
    }
  }

  onRecentlyUpdatedNextPage(stream: DashboardStream) {
    return (pageNum: number, pageSize: number) => {
      return this.seriesService.getRecentlyUpdatedSeries(pageNum, pageSize)
        .pipe(map(d => d.map(series => CardEntityFactory.recentlyUpdatedSeries(series))));
    }
  }

  loadDashboard() {
    this.isLoadingDashboard.set(true);
    this.streamsLoaded = 0;
    this.streamCount = 0;
    this.cdRef.markForCheck();

    this.dashboardService.getDashboardStreams().subscribe(streams => {
      this.streams = streams;
      this.streamCount = streams.length;
      this.streams.forEach(s => {
        switch (s.streamType) {
          case StreamType.OnDeck:
            s.api = this.seriesService.getOnDeck(1, 20)
                .pipe(
                  map(d => d.result.map(series => CardEntityFactory.series(series, { isOnDeck: true }))),
                  tap(() => this.increment()),
                  takeUntilDestroyed(this.destroyRef),
                  shareReplay({bufferSize: 1, refCount: true})
                );
            break;
          case StreamType.NewlyAdded:
            s.api = this.seriesService.getRecentlyAdded(1, 20)
                .pipe(
                  map(d => d.result.map(series => CardEntityFactory.series(series))),
                  tap(() => this.increment()),
                  takeUntilDestroyed(this.destroyRef),
                  shareReplay({bufferSize: 1, refCount: true})
                );
            break;
          case StreamType.RecentlyUpdated:
            s.api = this.seriesService.getRecentlyUpdatedSeries(1, 20)
              .pipe(
                map(d => d.map(series => CardEntityFactory.recentlyUpdatedSeries(series))),
                tap(() => this.increment()),
                takeUntilDestroyed(this.destroyRef),
                shareReplay({bufferSize: 1, refCount: true})
              );
            break;
          case StreamType.SmartFilter:
            s.api = this.filterUtilityService.decodeFilter(s.smartFilterEncoded!).pipe(
              switchMap(filter => {
                s.smartFilterDecoded = filter;
                switch (s.entityType) {
                  case FilterEntityType.Series:
                    return this.seriesService.getAllSeriesV2(0, 20, filter, QueryContext.Dashboard).pipe(map(d => d.result.map(series => CardEntityFactory.series(series))));
                  case FilterEntityType.ReadingList:
                    return this.readingListService.getAllReadingLists(filter, 0, 20).pipe(map(d => d.result.map(rl => CardEntityFactory.readingList(rl))));
                  case FilterEntityType.Person:
                    return this.personService.getAuthorsToBrowse(filter, 0, 20).pipe(map(d => d.result));
                  case FilterEntityType.Annotation:
                    return this.annotationService.getAllAnnotationsFiltered(filter, 0, 20).pipe(map(d => d.result));
                }
              }))
              .pipe(
                tap(() => this.increment()),
                takeUntilDestroyed(this.destroyRef),
                shareReplay({bufferSize: 1, refCount: true})
              );
            break;
          case StreamType.MoreInGenre:
            s.api = this.metadataService.getAllGenres([], QueryContext.Dashboard).pipe(
                map(genres => {
                  this.genre = genres[Math.floor(Math.random() * genres.length)];
                  return this.genre;
                }),
                switchMap(genre => this.recommendationService.getMoreIn(0, genre.id, 0, 30)),
                map(d => d.result.map(series => CardEntityFactory.series(series))),
                tap(() => this.increment()),
                takeUntilDestroyed(this.destroyRef),
                shareReplay({bufferSize: 1, refCount: true})
            );
            break;
        }
      });
      this.isLoadingDashboard.set(false);
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
    // if (onDeck) {
    //
    //   this.refreshStreams$.next();
    //   this.cdRef.markForCheck();
    // } else {
    //   // We can't just patch the streamId anymore since we aren't passing the data back
    //   //this.streams[index] = {...this.streams[index]};
    //   const entityMap = new Map<number, any>([data].map(e => [e.id, e]));
    //   this.bulkSelectionService.patchArray(this.streams[index], data);
    //   this.cdRef.markForCheck();
    // }

    // TODO: Need to figure out a better way to refresh just one stream (or patch in the changes into the stream
    // We can't just patch the streamId anymore since we aren't passing the data back
    this.refreshStreams$.next();
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
    switch (stream.entityType) {
      case FilterEntityType.Series:
        await this.router.navigateByUrl('all-series?' + stream.smartFilterEncoded);
        break;
      case FilterEntityType.ReadingList:
        await this.router.navigateByUrl('lists?' + stream.smartFilterEncoded);
        break;
      case FilterEntityType.Annotation:
        await this.router.navigateByUrl('browse/annotations?' + stream.smartFilterEncoded);
        break;
      case FilterEntityType.Person:
        await this.router.navigateByUrl('browse/people?' + stream.smartFilterEncoded);
        break;
    }
  }

  // TODO: See if we can put this into the carousel and have a custom tokens (not in the original list) to forward to a callback handler
  handleSectionClick(streamId: StreamId) {
    if (streamId === StreamId.RecentlyUpdatedSeries) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.recently-updated-title');
      const filter = this.metadataService.createDefaultFilterDto('series');
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SeriesSortField.LastChapterAdded;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.OnDeck) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.on-deck-title');

      const filter = this.metadataService.createDefaultFilterDto('series');
      filter.statements.push({field: SeriesFilterField.ReadProgress, comparison: FilterComparison.GreaterThan, value: '0'});
      filter.statements.push({field: SeriesFilterField.ReadProgress, comparison: FilterComparison.NotEqual, value: '100'});
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SeriesSortField.ReadProgress;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.NewlyAddedSeries) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.recently-added-title');
      const filter = this.metadataService.createDefaultFilterDto('series');
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SeriesSortField.Created;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    } else if (streamId === StreamId.MoreInGenre) {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.more-in-genre-title', {genre: this.genre?.title});
      const filter = this.metadataService.createDefaultFilterDto('series');
      filter.statements.push({field: SeriesFilterField.Genres, value: this.genre?.id + '', comparison: FilterComparison.MustContains});
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
    }
  }

  protected readonly SettingsTabId = SettingsTabId;
  protected readonly FilterEntityType = FilterEntityType;
}
