import { Injectable } from '@angular/core';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { Library } from '../_models/library';
import { MangaFormat } from '../_models/manga-format';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';

export enum Action {
  MarkAsRead = 0,
  MarkAsUnread = 1,
  ScanLibrary = 2,
  Delete = 3,
  Edit = 4,
  Info = 5,
  RefreshMetadata = 6,
  Download = 7,
  /**
   * @deprecated This is no longer supported. Use the dedicated page instead
   */
  Bookmarks = 8,
  IncognitoRead = 9,
  AddToReadingList = 10,
  AddToCollection = 11,
  /**
   * Essentially a download, but handled differently. Needed so card bubbles it up for handling
   */
  DownloadBookmark = 12
}

export interface ActionItem<T> {
  title: string;
  action: Action;
  callback: (action: Action, data: T) => void;
  requiresAdmin: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ActionFactoryService {

  libraryActions: Array<ActionItem<Library>> = [];

  seriesActions: Array<ActionItem<Series>> = [];

  volumeActions: Array<ActionItem<Volume>> = [];

  chapterActions: Array<ActionItem<Chapter>> = [];

  collectionTagActions: Array<ActionItem<CollectionTag>> = [];

  readingListActions: Array<ActionItem<ReadingList>> = [];

  bookmarkActions: Array<ActionItem<Series>> = [];

  isAdmin = false;
  hasDownloadRole = false;

  constructor(private accountService: AccountService) {
    this.accountService.currentUser$.subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasDownloadRole = this.accountService.hasDownloadRole(user);
      } else {
        this._resetActions();
        return; // If user is logged out, we don't need to do anything
      }

      this._resetActions();

      if (this.isAdmin) {
        this.collectionTagActions.push({
          action: Action.Edit,
          title: 'Edit',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.ScanLibrary,
          title: 'Scan Series',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.RefreshMetadata,
          title: 'Refresh Covers',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.Delete,
          title: 'Delete',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.AddToCollection,
          title: 'Add to Collection',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.Edit,
          title: 'Edit',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.libraryActions.push({
          action: Action.ScanLibrary,
          title: 'Scan Library',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.libraryActions.push({
          action: Action.RefreshMetadata,
          title: 'Refresh Covers',
          callback: this.dummyCallback,
          requiresAdmin: true
        });
    
        this.chapterActions.push({
          action: Action.Edit,
          title: 'Details',
          callback: this.dummyCallback,
          requiresAdmin: false
        });
      }

      if (this.hasDownloadRole || this.isAdmin) {
        this.volumeActions.push({
          action: Action.Download,
          title: 'Download',
          callback: this.dummyCallback,
          requiresAdmin: false
        });

        this.chapterActions.push({
          action: Action.Download,
          title: 'Download',
          callback: this.dummyCallback,
          requiresAdmin: false
        });
      }
    });
  }

  getLibraryActions(callback: (action: Action, library: Library) => void) {
    const actions = this.libraryActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getSeriesActions(callback: (action: Action, series: Series) => void) {
    const actions = this.seriesActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getVolumeActions(callback: (action: Action, volume: Volume) => void) {
    const actions = this.volumeActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getChapterActions(callback: (action: Action, chapter: Chapter) => void) {
    const actions = this.chapterActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getCollectionTagActions(callback: (action: Action, collectionTag: CollectionTag) => void) {
    const actions = this.collectionTagActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getReadingListActions(callback: (action: Action, readingList: ReadingList) => void) {
    const actions = this.readingListActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  getBookmarkActions(callback: (action: Action, series: Series) => void) {
    const actions = this.bookmarkActions.map(a => {return {...a}});
    actions.forEach(action => action.callback = callback);
    return actions;
  }

  filterBookmarksForFormat(action: ActionItem<Series>, series: Series) {
    if (action.action === Action.Bookmarks && series?.format === MangaFormat.EPUB) return false;
    return true;
  }

  dummyCallback(action: Action, data: any) {}

  _resetActions() {
    this.libraryActions = [];

    this.collectionTagActions = [];
    
    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
          requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
          requiresAdmin: false
      }, 
      {
        action: Action.AddToReadingList,
        title: 'Add to Reading List',
        callback: this.dummyCallback,
        requiresAdmin: false
      }
    ];

    this.volumeActions = [
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
          requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.AddToReadingList,
        title: 'Add to Reading List',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.IncognitoRead,
        title: 'Read Incognito',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.Edit,
        title: 'Details',
        callback: this.dummyCallback,
        requiresAdmin: false
      }
    ];

    this.chapterActions = [
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.IncognitoRead,
        title: 'Read Incognito',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.AddToReadingList,
        title: 'Add to Reading List',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.Delete,
        title: 'Delete',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
    ];

    this.bookmarkActions = [
      {
        action: Action.DownloadBookmark,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.Delete,
        title: 'Clear',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
    ]
  }
}
