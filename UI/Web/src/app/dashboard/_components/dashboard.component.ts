import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {Router, RouterLink} from '@angular/router';
import {Observable, of, ReplaySubject, switchMap} from 'rxjs';
import {map, shareReplay, take, tap} from 'rxjs/operators';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {SeriesAddedEvent} from 'src/app/_models/events/series-added-event';
import {SeriesRemovedEvent} from 'src/app/_models/events/series-removed-event';
import {Library} from 'src/app/_models/library';
import {RecentlyAddedItem} from 'src/app/_models/recently-added-item';
import {Series} from 'src/app/_models/series';
import {SortField} from 'src/app/_models/metadata/series-filter';
import {SeriesGroup} from 'src/app/_models/series-group';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CardItemComponent} from '../../cards/card-item/card-item.component';
import {SeriesCardComponent} from '../../cards/series-card/series-card.component';
import {CarouselReelComponent} from '../../carousel/_components/carousel-reel/carousel-reel.component';
import {AsyncPipe, NgForOf, NgIf, NgSwitch, NgSwitchCase, NgTemplateOutlet} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";
import {DashboardService} from "../../_services/dashboard.service";
import {MetadataService} from "../../_services/metadata.service";
import {RecommendationService} from "../../_services/recommendation.service";
import {Genre} from "../../_models/metadata/genre";

export enum StreamType {
  OnDeck = 1,
  RecentlyUpdated = 2,
  NewlyAdded = 3,
  SmartFilter = 4,
  MoreInGenre = 5
}

export interface DashboardStream {
  id: number;
  name: string;
  isProvided: boolean;
  api: Observable<any[]>;
  smartFilterId: number;
  smartFilterEncoded?: string;
  streamType: StreamType;
  order: number;
  visible: boolean;
}

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, NgIf, RouterLink, CarouselReelComponent, SeriesCardComponent,
    CardItemComponent, AsyncPipe, TranslocoDirective, NgSwitchCase, NgSwitch, NgForOf, NgTemplateOutlet]
})
export class DashboardComponent implements OnInit {

  /**
   * By default, 0, but if non-zero, will limit all API calls to library id
   */
  @Input() libraryId: number = 0;

  libraries$: Observable<Library[]> = of([]);
  isLoading = true;

  isAdmin$: Observable<boolean> = of(false);

  recentlyUpdatedSeries: SeriesGroup[] = [];
  inProgress: Series[] = [];
  recentlyAddedSeries: Series[] = [];
  streams: Array<DashboardStream> = [];
  genre: Genre | undefined;

  /**
   * We use this Replay subject to slow the amount of times we reload the UI
   */
  private loadRecentlyAdded$: ReplaySubject<void> = new ReplaySubject<void>();
  private readonly destroyRef = inject(DestroyRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);
  private readonly recommendationService = inject(RecommendationService);
  protected readonly StreamId = StreamType;



  constructor(public accountService: AccountService, private libraryService: LibraryService,
    private seriesService: SeriesService, private router: Router,
    private titleService: Title, public imageService: ImageService,
    private messageHub: MessageHubService, private readonly cdRef: ChangeDetectorRef,
              private dashboardService: DashboardService) {

    this.dashboardService.getDashboardStreams().subscribe(streams => {
      this.streams = streams;
      this.streams.forEach(s => {
        switch (s.streamType) {
          case StreamType.OnDeck:
            s.api = this.seriesService.getOnDeck(0, 1, 20)
              .pipe(map(d => d.result), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.NewlyAdded:
            s.api = this.seriesService.getRecentlyAdded(1, 20)
              .pipe(map(d => d.result), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.RecentlyUpdated:
            s.api = this.seriesService.getRecentlyUpdatedSeries();
            break;
          case StreamType.SmartFilter:
            s.api = this.seriesService.getAllSeriesV2(0, 20, this.filterUtilityService.decodeSeriesFilter(s.smartFilterEncoded!))
              .pipe(map(d => d.result), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
            break;
          case StreamType.MoreInGenre:
            s.api = this.metadataService.getAllGenres().pipe(
              map(genres => {
                this.genre = genres[Math.floor(Math.random() * genres.length)];
                return this.genre;
              }),
              switchMap(genre => this.recommendationService.getMoreIn(0, genre.id, 0, 30)),
              map(p => p.result),
              takeUntilDestroyed(this.destroyRef),
              shareReplay({bufferSize: 1, refCount: true})
            );
            break;
        }
      })
    });


    // TODO: Solve how Websockets will work with these dyanamic streams
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res.event === EVENTS.SeriesAdded) {
        const seriesAddedEvent = res.payload as SeriesAddedEvent;


        this.seriesService.getSeries(seriesAddedEvent.seriesId).subscribe(series => {
          if (this.recentlyAddedSeries.filter(s => s.id === series.id).length > 0) return;
          this.recentlyAddedSeries = [series, ...this.recentlyAddedSeries];
          this.cdRef.markForCheck();
        });
      } else if (res.event === EVENTS.SeriesRemoved) {
        const seriesRemovedEvent = res.payload as SeriesRemovedEvent;

        this.inProgress = this.inProgress.filter(item => item.id != seriesRemovedEvent.seriesId);
        this.recentlyAddedSeries = this.recentlyAddedSeries.filter(item => item.id != seriesRemovedEvent.seriesId);
        this.recentlyUpdatedSeries = this.recentlyUpdatedSeries.filter(item => item.seriesId != seriesRemovedEvent.seriesId);
        this.cdRef.markForCheck();
      } else if (res.event === EVENTS.ScanSeries) {
        // We don't have events for when series are updated, but we do get events when a scan update occurs. Refresh recentlyAdded at that time.
        this.loadRecentlyAdded$.next();
      }
    });

    this.isAdmin$ = this.accountService.currentUser$.pipe(
      takeUntilDestroyed(this.destroyRef),
      map(user => (user && this.accountService.hasAdminRole(user)) || false),
      shareReplay({bufferSize: 1, refCount: true})
    );

    // this.loadRecentlyAdded$.pipe(debounceTime(1000), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
    //   this.loadRecentlyUpdated();
    //   this.loadRecentlyAddedSeries();
    //   this.cdRef.markForCheck();
    // });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - Dashboard');
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.libraries$ = this.libraryService.getLibraries().pipe(take(1), takeUntilDestroyed(this.destroyRef), tap((libs) => {
      this.isLoading = false;
      this.cdRef.markForCheck();
    }));

    //this.reloadSeries();
  }

  reloadSeries() {
    this.loadOnDeck();
    this.loadRecentlyUpdated();
    this.loadRecentlyAddedSeries();
  }

  reloadStream(streamId: number) {

  }

  reloadInProgress(series: Series | number) {
    this.loadOnDeck(); // TODO: Use combineLatest so we can emit when to reload an api
  }

  loadOnDeck() {
    let api = this.seriesService.getOnDeck(0, 1, 30);
    if (this.libraryId > 0) {
      api = this.seriesService.getOnDeck(this.libraryId, 1, 30);
    }
    api.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((updatedSeries) => {
      this.inProgress = updatedSeries.result;
      this.cdRef.markForCheck();
    });
  }

  loadRecentlyAddedSeries() {
    let api = this.seriesService.getRecentlyAdded(1, 30);
    if (this.libraryId > 0) {
      const filter = this.filterUtilityService.createSeriesV2Filter();
      filter.statements.push({field: FilterField.Libraries, value: this.libraryId + '', comparison: FilterComparison.Equal});
      api = this.seriesService.getRecentlyAdded(1, 30, filter);
    }
    api.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((updatedSeries) => {
      this.recentlyAddedSeries = updatedSeries.result;
      this.cdRef.markForCheck();
    });
  }


  loadRecentlyUpdated() {
    let api = this.seriesService.getRecentlyUpdatedSeries();
    if (this.libraryId > 0) {
      api = this.seriesService.getRecentlyUpdatedSeries();
    }
    api.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(updatedSeries => {
      this.recentlyUpdatedSeries = updatedSeries.filter(group => {
        if (this.libraryId === 0) return true;
        return group.libraryId === this.libraryId;
      });
      this.cdRef.markForCheck();
    });
  }

  async handleRecentlyAddedChapterClick(item: RecentlyAddedItem) {
    await this.router.navigate(['library', item.libraryId, 'series', item.seriesId]);
  }

  async handleFilterSectionClick(stream: DashboardStream) {
    await this.router.navigateByUrl('all-series?' + stream.smartFilterEncoded);
  }

  handleSectionClick(sectionTitle: string) {
    if (sectionTitle.toLowerCase() === 'recently updated series') {
      const params: any = {};
      params['page'] = 1;
      params['title'] = 'Recently Updated';
      const filter = this.filterUtilityService.createSeriesV2Filter();
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.LastChapterAdded;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params)
    } else if (sectionTitle.toLowerCase() === 'on deck') {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.on-deck-title');

      const filter = this.filterUtilityService.createSeriesV2Filter();
      filter.statements.push({field: FilterField.ReadProgress, comparison: FilterComparison.GreaterThan, value: '0'});
      filter.statements.push({field: FilterField.ReadProgress, comparison: FilterComparison.LessThan, value: '100'});
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.LastChapterAdded;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params)
    } else if (sectionTitle.toLowerCase() === 'newly added series') {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('dashboard.recently-added-title');
      const filter = this.filterUtilityService.createSeriesV2Filter();
      if (filter.sortOptions) {
        filter.sortOptions.sortField = SortField.Created;
        filter.sortOptions.isAscending = false;
      }
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params)
    } else if (sectionTitle.toLowerCase() === 'more in genre') {
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('more-in-genre-title', {genre: this.genre?.title});
      const filter = this.filterUtilityService.createSeriesV2Filter();
      filter.statements.push({field: FilterField.Genres, value: this.genre?.id + '', comparison: FilterComparison.MustContains});
      this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params)
    }
  }

  removeFromArray(arr: Array<any>, element: any) {
    const index = arr.indexOf(element);
    if (index >= 0) {
      arr.splice(index);
    }
  }
}
