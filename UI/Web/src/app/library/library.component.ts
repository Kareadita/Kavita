import { Component, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';
import { InProgressChapter } from '../_models/in-progress-chapter';
import { Library } from '../_models/library';
import { Series } from '../_models/series';
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

  recentlyAdded: Series[] = [];
  inProgress: Series[] = [];
  continueReading: InProgressChapter[] = [];

  private readonly onDestroy = new Subject<void>();

  seriesTrackBy = (index: number, item: any) => `${item.name}_${item.pagesRead}`;

  constructor(public accountService: AccountService, private libraryService: LibraryService, 
    private seriesService: SeriesService, private router: Router, 
    private titleService: Title, public imageService: ImageService, 
    private messageHub: MessageHubService) {
      this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(res => {
        if (res.event === EVENTS.SeriesAdded) {
          const seriesAddedEvent = res.payload as SeriesAddedEvent;
          this.seriesService.getSeries(seriesAddedEvent.seriesId).subscribe(series => {
            this.recentlyAdded.unshift(series);
          });
        } else if (res.event === EVENTS.SeriesRemoved) {
          const seriesRemovedEvent = res.payload as SeriesRemovedEvent;
          this.recentlyAdded = this.recentlyAdded.filter(item => item.id != seriesRemovedEvent.seriesId);
          this.inProgress = this.inProgress.filter(item => item.id != seriesRemovedEvent.seriesId);
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
    this.loadRecentlyAdded();
    this.loadOnDeck();
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
    this.seriesService.getRecentlyAdded(0, 0, 20).pipe(takeUntil(this.onDestroy)).subscribe(updatedSeries => {
      this.recentlyAdded = updatedSeries.result;
    });
  }

  handleSectionClick(sectionTitle: string) {
    if (sectionTitle.toLowerCase() === 'collections') {
      this.router.navigate(['collections']);
    } else if (sectionTitle.toLowerCase() === 'recently added') {
      this.router.navigate(['recently-added']);
    } else if (sectionTitle.toLowerCase() === 'on deck') {
      this.router.navigate(['on-deck']);
    } 
  }

  removeFromArray(arr: Array<any>, element: any) {
    const index = arr.indexOf(element);
    if (index >= 0) {
      arr.splice(index);
    }
  }
}
