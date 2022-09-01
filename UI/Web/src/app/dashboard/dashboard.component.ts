import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { Observable, of, ReplaySubject, Subject } from 'rxjs';
import { debounceTime, map, take, takeUntil, tap, shareReplay } from 'rxjs/operators';
import { FilterQueryParam } from '../shared/_services/filter-utilities.service';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';
import { Library } from '../_models/library';
import { RecentlyAddedItem } from '../_models/recently-added-item';
import { Series } from '../_models/series';
import { SortField } from '../_models/series-filter';
import { SeriesGroup } from '../_models/series-group';
import { AccountService } from '../_services/account.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { MessageHubService, EVENTS } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent implements OnInit, OnDestroy {

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

  private readonly onDestroy = new Subject<void>();

  /**
   * We use this Replay subject to slow the amount of times we reload the UI
   */
  private loadRecentlyAdded$: ReplaySubject<void> = new ReplaySubject<void>();

  constructor(public accountService: AccountService, private libraryService: LibraryService, 
    private seriesService: SeriesService, private router: Router, 
    private titleService: Title, public imageService: ImageService, 
    private messageHub: MessageHubService, private readonly cdRef: ChangeDetectorRef) {

      this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(res => {
        if (res.event === EVENTS.SeriesAdded) {
          const seriesAddedEvent = res.payload as SeriesAddedEvent;

          this.seriesService.getSeries(seriesAddedEvent.seriesId).subscribe(series => {
            this.recentlyAddedSeries.unshift(series);
            this.cdRef.detectChanges();
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
        takeUntil(this.onDestroy), 
        map(user => (user && this.accountService.hasAdminRole(user)) || false), 
        shareReplay()
      );

      this.loadRecentlyAdded$.pipe(debounceTime(1000), takeUntil(this.onDestroy)).subscribe(() => {
        this.loadRecentlyUpdated();
        this.loadRecentlyAddedSeries();
        this.cdRef.markForCheck();
      });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - Dashboard');
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.libraries$ = this.libraryService.getLibrariesForMember().pipe(take(1), tap((libs) => {
      this.isLoading = false;
      this.cdRef.markForCheck();
    }));

    this.reloadSeries();
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  reloadSeries() {
    this.loadOnDeck();
    this.loadRecentlyUpdated();
    this.loadRecentlyAddedSeries();
  }

  reloadInProgress(series: Series | boolean) {
    if (series === true || series === false) {
      if (!series) {return;}
    }
    // If the update to Series doesn't affect the requirement to be in this stream, then ignore update request
    const seriesObj = (series as Series);
    if (seriesObj.pagesRead !== seriesObj.pages && seriesObj.pagesRead !== 0) {
      return;
    }

    this.loadOnDeck();
  }

  loadOnDeck() {
    let api = this.seriesService.getOnDeck(0, 1, 30);
    if (this.libraryId > 0) {
      api = this.seriesService.getOnDeck(this.libraryId, 1, 30);
    }
    api.pipe(takeUntil(this.onDestroy)).subscribe((updatedSeries) => {
      this.inProgress = updatedSeries.result;
      this.cdRef.markForCheck();
    });
  }

  loadRecentlyAddedSeries() {
    let api = this.seriesService.getRecentlyAdded(0, 1, 30);
    if (this.libraryId > 0) {
      api = this.seriesService.getRecentlyAdded(this.libraryId, 1, 30);
    }
    api.pipe(takeUntil(this.onDestroy)).subscribe((updatedSeries) => {
      this.recentlyAddedSeries = updatedSeries.result;
      this.cdRef.markForCheck();
    });
  }


  loadRecentlyUpdated() {
    let api = this.seriesService.getRecentlyUpdatedSeries();
    if (this.libraryId > 0) {
      api = this.seriesService.getRecentlyUpdatedSeries();
    }
    api.pipe(takeUntil(this.onDestroy)).subscribe(updatedSeries => {
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
      this.router.navigate(['all-series'], {queryParams: params});
    } else if (sectionTitle.toLowerCase() === 'on deck') {
      const params: any = {};
      params[FilterQueryParam.ReadStatus] = 'true,false,false';
      params[FilterQueryParam.SortBy] = SortField.LastChapterAdded + ',false'; // sort by last chapter added, desc
      params[FilterQueryParam.Page] = 1;
      this.router.navigate(['all-series'], {queryParams: params});
    }else if (sectionTitle.toLowerCase() === 'newly added series') {
      const params: any = {};
      params[FilterQueryParam.SortBy] = SortField.Created + ',false'; // sort by created, desc
      params[FilterQueryParam.Page] = 1;
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
