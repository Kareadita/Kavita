import { Injectable } from '@angular/core';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { Library } from '../_models/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';

export enum Action {
  AddTo = -2,
  Others = -1,
  /**
   * Mark entity as read
   */
  MarkAsRead = 0,
  /**
   * Mark entity as unread
   */
  MarkAsUnread = 1,
  /**
   * Invoke a Scan on Series/Library
   */
  Scan = 2,
  /**
   * Delete the entity
   */
  Delete = 3,
  /**
   * Open edit modal
   */
  Edit = 4,
  /**
   * Open details modal
   */
  Info = 5,
  /**
   * Invoke a refresh covers
   */
  RefreshMetadata = 6,
  /**
   * Download the entity
   */
  Download = 7,
  /**
   * Invoke an Analyze Files which calculates word count
   */
  AnalyzeFiles = 8,
  /**
   * Read in incognito mode aka no progress tracking
   */
  IncognitoRead = 9,
  /**
   * Add to reading list
   */
  AddToReadingList = 10,
  /**
   * Add to collection
   */
  AddToCollection = 11,
  /**
   * Essentially a download, but handled differently. Needed so card bubbles it up for handling
   */
  DownloadBookmark = 12,
  /**
   * Open Series detail page for said series
   */
  ViewSeries = 13,
  /**
   * Open the reader for entity
   */
  Read = 14,
  /**
   * Add to user's Want to Read List
   */
  AddToWantToReadList = 15,
  /**
   * Remove from user's Want to Read List
   */
  RemoveFromWantToReadList = 16,
}

export interface ActionItem<T> {
  title: string;
  action: Action;
  callback: (action: Action, data: T) => void;
  requiresAdmin: boolean;
  children: Array<ActionItem<T>>;
}

@Injectable({
  providedIn: 'root',
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
    this.accountService.currentUser$.subscribe((user) => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasDownloadRole = this.accountService.hasDownloadRole(user);
      } else {
        this._resetActions();
        return; // If user is logged out, we don't need to do anything
      }

      this._resetActions();
    });
  }

  getLibraryActions(callback: (action: Action, library: Library) => void) {
    const actions = this.libraryActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getSeriesActions(callback: (action: Action, series: Series) => void) {
    const actions = this.seriesActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getVolumeActions(callback: (action: Action, volume: Volume) => void) {
    const actions = this.volumeActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getChapterActions(callback: (action: Action, chapter: Chapter) => void) {
    const actions = this.chapterActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getCollectionTagActions(
    callback: (action: Action, collectionTag: CollectionTag) => void
  ) {
    const actions = this.collectionTagActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getReadingListActions(
    callback: (action: Action, readingList: ReadingList) => void
  ) {
    const actions = this.readingListActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  getBookmarkActions(callback: (action: Action, series: Series) => void) {
    const actions = this.bookmarkActions.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => (this.appyCallback(action, callback)));
    return actions;
  }

  dummyCallback(action: Action, data: any) {}

  _resetActions() {
    this.libraryActions = [
      {
        action: Action.Scan,
        title: 'Scan Library',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      },
      {
        action: Action.RefreshMetadata,
        title: 'Refresh Covers',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      },
      {
        action: Action.AnalyzeFiles,
        title: 'Analyze Files',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      }
    ];

    this.collectionTagActions = [
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      }
    ];

    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.AddTo,
        title: 'Add to',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.AddToWantToReadList,
            title: 'Add to Want To Read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: []
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'Remove from Want To Read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: []
          },
          {
            action: Action.AddToReadingList,
            title: 'Add to Reading List',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: []
          },
          {
            action: Action.AddToCollection,
            title: 'Add to Collection',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: []
          }
        ],
      },
      {
        action: Action.Scan,
        title: 'Scan Series',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      },
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: []
      },
      {
        action: Action.Others,
        title: 'Others',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'Refresh Covers',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: []
          },
          {
            action: Action.AnalyzeFiles,
            title: 'Analyze Files',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: []
          },
          {
            action: Action.Delete,
            title: 'Delete',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: []
          }
        ]
      }
    ];

    this.volumeActions = [
      {
        action: Action.IncognitoRead,
        title: '(Read Incognito)',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.AddToReadingList,
        title: 'Add to Reading List',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Edit,
        title: 'Details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Download,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      }
    ];

    this.chapterActions = [
      {
        action: Action.IncognitoRead,
        title: '(Read Incognito)',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.AddToReadingList,
        title: 'Add to Reading List',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Edit,
        title: 'Details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Download,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      }
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Delete,
        title: 'Delete',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
    ];

    this.bookmarkActions = [
      {
        action: Action.ViewSeries,
        title: 'View Series',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.DownloadBookmark,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
      {
        action: Action.Delete,
        title: 'Clear',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: []
      },
    ];
  }

  private appyCallback(action: ActionItem<any>, callback: (action: Action, data: any) => void) {
    action.callback = callback;

    if (action.children === null || action.children?.length === 0) return;

    action.children?.forEach((childAction) => {
      this.appyCallback(childAction, callback);
    });
  }
}