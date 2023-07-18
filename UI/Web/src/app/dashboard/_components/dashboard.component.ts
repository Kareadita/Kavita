import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { Observable, of, ReplaySubject } from 'rxjs';
import { debounceTime, map, take, tap, shareReplay } from 'rxjs/operators';
import { FilterQueryParam } from 'src/app/shared/_services/filter-utilities.service';
import { SeriesAddedEvent } from 'src/app/_models/events/series-added-event';
import { SeriesRemovedEvent } from 'src/app/_models/events/series-removed-event';
import { Library } from 'src/app/_models/library';
import { RecentlyAddedItem } from 'src/app/_models/recently-added-item';
import { Series } from 'src/app/_models/series';
import { SortField } from 'src/app/_models/metadata/series-filter';
import { SeriesGroup } from 'src/app/_models/series-group';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { MessageHubService, EVENTS } from 'src/app/_services/message-hub.service';
import { SeriesService } from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { CardItemComponent } from '../../cards/card-item/card-item.component';
import { SeriesCardComponent } from '../../cards/series-card/series-card.component';
import { CarouselReelComponent } from '../../carousel/_components/carousel-reel/carousel-reel.component';
import { NgIf, AsyncPipe } from '@angular/common';
import { SideNavCompanionBarComponent } from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [SideNavCompanionBarComponent, NgIf, RouterLink, CarouselReelComponent, SeriesCardComponent, CardItemComponent, AsyncPipe]
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

  /**
   * We use this Replay subject to slow the amount of times we reload the UI
   */
  private loadRecentlyAdded$: ReplaySubject<void> = new ReplaySubject<void>();
  private readonly destroyRef = inject(DestroyRef);

  constructor(public accountService: AccountService, private libraryService: LibraryService,
    private seriesService: SeriesService, private router: Router,
    private titleService: Title, public imageService: ImageService,
    private messageHub: MessageHubService, private readonly cdRef: ChangeDetectorRef) {

      this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
        if (res.event === EVENTS.SeriesAdded) {
          const seriesAddedEvent = res.payload as SeriesAddedEvent;


          this.seriesService.getSeries(seriesAddedEvent.seriesId).subscribe(series => {
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
        shareReplay()
      );

      this.loadRecentlyAdded$.pipe(debounceTime(1000), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
        this.loadRecentlyUpdated();
        this.loadRecentlyAddedSeries();
        this.cdRef.markForCheck();
      });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - Dashboard');
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.libraries$ = this.libraryService.getLibraries().pipe(take(1), takeUntilDestroyed(this.destroyRef), tap((libs) => {
      this.isLoading = false;
      this.cdRef.markForCheck();
    }));

    this.reloadSeries();
  }

  reloadSeries() {
    this.loadOnDeck();
    this.loadRecentlyUpdated();
    this.loadRecentlyAddedSeries();
  }

  reloadInProgress(series: Series | number) {
    this.loadOnDeck();
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
    let api = this.seriesService.getRecentlyAdded(0, 1, 30);
    if (this.libraryId > 0) {
      api = this.seriesService.getRecentlyAdded(this.libraryId, 1, 30);
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

  handleRecentlyAddedChapterClick(item: RecentlyAddedItem) {
    this.router.navigate(['library', item.libraryId, 'series', item.seriesId]);
  }

  handleSectionClick(sectionTitle: string) {
    if (sectionTitle.toLowerCase() === 'recently updated series') {
      const params: any = {};
      params[FilterQueryParam.SortBy] = SortField.LastChapterAdded + ',false'; // sort by last chapter added, desc
      params[FilterQueryParam.Page] = 1;
      params['title'] = 'Recently Updated';
      this.router.navigate(['all-series'], {queryParams: params});
    } else if (sectionTitle.toLowerCase() === 'on deck') {
      const params: any = {};
      params[FilterQueryParam.ReadStatus] = 'true,false,false';
      params[FilterQueryParam.SortBy] = SortField.LastChapterAdded + ',false'; // sort by last chapter added, desc
      params[FilterQueryParam.Page] = 1;
      params['title'] = 'On Deck';
      this.router.navigate(['all-series'], {queryParams: params});
    }else if (sectionTitle.toLowerCase() === 'newly added series') {
      const params: any = {};
      params[FilterQueryParam.SortBy] = SortField.Created + ',false'; // sort by created, desc
      params[FilterQueryParam.Page] = 1;
      params['title'] = 'Newly Added';
      this.router.navigate(['all-series'], {queryParams: params});
    }
  }

  removeFromArray(arr: Array<any>, element: any) {
    const index = arr.indexOf(element);
    if (index >= 0) {
      arr.splice(index);
    }
  }
}
