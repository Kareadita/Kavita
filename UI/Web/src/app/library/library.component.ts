import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { EditCollectionTagsComponent } from '../_modals/edit-collection-tags/edit-collection-tags.component';
import { CollectionTag } from '../_models/collection-tag';
import { InProgressChapter } from '../_models/in-progress-chapter';
import { Library } from '../_models/library';
import { Series } from '../_models/series';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { CollectionTagService } from '../_services/collection-tag.service';
import { LibraryService } from '../_services/library.service';
import { SeriesService } from '../_services/series.service';

@Component({
  selector: 'app-library',
  templateUrl: './library.component.html',
  styleUrls: ['./library.component.scss']
})
export class LibraryComponent implements OnInit {

  user: User | undefined;
  libraries: Library[] = [];
  isLoading = false;
  isAdmin = false;

  recentlyAdded: Series[] = [];
  inProgress: Series[] = [];
  continueReading: InProgressChapter[] = [];
  collectionTags: CollectionTag[] = [];
  collectionTagActions: ActionItem<CollectionTag>[] = [];

  seriesTrackBy = (index: number, item: any) => `${item.name}_${item.pagesRead}`;

  constructor(public accountService: AccountService, private libraryService: LibraryService, private seriesService: SeriesService, private actionFactoryService: ActionFactoryService, private collectionService: CollectionTagService, private router: Router, private modalService: NgbModal) { }

  ngOnInit(): void {
    this.isLoading = true;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      this.isAdmin = this.accountService.hasAdminRole(this.user);
      this.libraryService.getLibrariesForMember().subscribe(libraries => {
        this.libraries = libraries;
        this.isLoading = false;
      });
    });

    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));

    this.reloadSeries();
  }

  reloadSeries() {
    this.seriesService.getRecentlyAdded(0, 0, 20).subscribe(updatedSeries => {
      this.recentlyAdded = updatedSeries.result;
    });

    this.seriesService.getInProgress().subscribe((updatedSeries) => {
      this.inProgress = updatedSeries;
    });

    this.reloadTags();
  }

  reloadInProgress(series: Series | boolean) {
    if (series === true || series === false) {
      if (!series) {return;}
    }

    if ((series as Series).pagesRead !== (series as Series).pages && (series as Series).pagesRead !== 0) {
      return;
    }

    this.seriesService.getInProgress().subscribe((updatedSeries) => {
      this.inProgress = updatedSeries;
    });
    
    this.reloadTags();
  }

  reloadTags() {
    this.collectionService.allTags().subscribe(tags => {
      this.collectionTags = tags;
    });
  }

  handleSectionClick(sectionTitle: string) {
    if (sectionTitle.toLowerCase() === 'collections') {
      this.router.navigate(['collections']);
    } else if (sectionTitle.toLowerCase() === 'recently added') {
      this.router.navigate(['recently-added']);
    }
  }

  loadCollection(item: CollectionTag) {
    this.router.navigate(['collections', item.id]);
  }

  handleCollectionActionCallback(action: Action, collectionTag: CollectionTag) {
    switch (action) {
      case(Action.Edit):
        const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
        modalRef.componentInstance.tag = collectionTag;
        modalRef.closed.subscribe((reloadNeeded: boolean) => {
          if (reloadNeeded) {
            // Reload tags
            this.reloadTags();
          }
        });
        break;
      default:
        break;
    }
  }

}
