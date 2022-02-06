import { Component, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';
import { Library } from '../_models/library';
import { RecentlyAddedItem } from '../_models/recently-added-item';
import { Series } from '../_models/series';
import { SeriesGroup } from '../_models/series-group';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { EVENTS, MessageHubService } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-library',
  templateUrl: './library.component.html',
  styleUrls: ['./library.component.scss']
})
export class LibraryComponent implements OnInit, OnDestroy {

  user: User | undefined;
  libraries: Library[] = [];
  isLoading = false;
  isAdmin = false;

  recentlyUpdatedSeries: SeriesGroup[] = [];
  recentlyAddedChapters: RecentlyAddedItem[] = [];
  inProgress: Series[] = [];

  private readonly onDestroy = new Subject<void>();

  seriesTrackBy = (index: number, item: any) => `${item.name}_${item.pagesRead}`;

  constructor(public accountService: AccountService, private libraryService: LibraryService, 
    private seriesService: SeriesService, private router: Router, 
    private titleService: Title, public imageService: ImageService, 
    private messageHub: MessageHubService) {
      this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(res => {
        if (res.event === EVENTS.SeriesAdded) {
          const seriesAddedEvent = res.payload as SeriesAddedEvent;
          this.loadRecentlyAdded();
        } else if (res.event === EVENTS.SeriesRemoved) {
          const seriesRemovedEvent = res.payload as SeriesRemovedEvent;
          this.inProgress = this.inProgress.filter(item => item.id != seriesRemovedEvent.seriesId);
          
          this.recentlyUpdatedSeries = this.recentlyUpdatedSeries.filter(item => item.seriesId != seriesRemovedEvent.seriesId);
          this.recentlyAddedChapters = this.recentlyAddedChapters.filter(item => item.seriesId != seriesRemovedEvent.seriesId);
        } else if (res.event === EVENTS.ScanSeries) {
          // We don't have events for when series are updated, but we do get events when a scan update occurs. Refresh recentlyAdded at that time.
          this.loadRecentlyAdded();
        }
      });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - Dashboard');
    this.isLoading = true;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      this.isAdmin = this.accountService.hasAdminRole(this.user);
      this.libraryService.getLibrariesForMember().pipe(take(1)).subscribe(libraries => {
        this.libraries = libraries;
        this.isLoading = false;
      });
    });

    this.reloadSeries();
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  reloadSeries() {
    this.loadOnDeck();
    this.loadRecentlyAdded();
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
    this.seriesService.getOnDeck().pipe(takeUntil(this.onDestroy)).subscribe((updatedSeries) => {
      this.inProgress = updatedSeries.result;
    });
  }


  loadRecentlyAdded() {
    this.seriesService.getRecentlyUpdatedSeries().pipe(takeUntil(this.onDestroy)).subscribe(updatedSeries => {
      this.recentlyUpdatedSeries = updatedSeries;
    });

    this.seriesService.getRecentlyAddedChapters().pipe(takeUntil(this.onDestroy)).subscribe(updatedSeries => {
      this.recentlyAddedChapters = updatedSeries;
    });
  }

  handleRecentlyAddedChapterClick(item: RecentlyAddedItem) {
    this.router.navigate(['library', item.libraryId, 'series', item.seriesId]);
  }

  handleSectionClick(sectionTitle: string) {
    if (sectionTitle.toLowerCase() === 'collections') {
      this.router.navigate(['collections']);
    } else if (sectionTitle.toLowerCase() === 'recently added') {
      this.router.navigate(['recently-added']);
    } else if (sectionTitle.toLowerCase() === 'on deck') {
      this.router.navigate(['on-deck']);
    } else if (sectionTitle.toLowerCase() === 'libraries') {
      this.router.navigate(['all-series']);
    } 
  }

  removeFromArray(arr: Array<any>, element: any) {
    const index = arr.indexOf(element);
    if (index >= 0) {
      arr.splice(index);
    }
  }
}
