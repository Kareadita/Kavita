import { Injectable } from '@angular/core';
import { map, Observable, shareReplay } from 'rxjs';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { Device } from '../_models/device/device';
import { Library } from '../_models/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';
import { DeviceService } from './device.service';

export enum Action {
  Submenu = -1,
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
  /**
   * Send to a device
   */
  SendTo = 17,
}

export interface ActionItem<T> {
  title: string;
  action: Action;
  callback: (action: ActionItem<T>, data: T) => void;
  requiresAdmin: boolean;
  children: Array<ActionItem<T>>;
  /**
   * An optional class which applies to an item. ie) danger on a delete action
   */
  class?: string;
  /**
   * Indicates that there exists a separate list will be loaded from an API.
   * Rule: If using this, only one child should exist in children with the Action for dynamicList.
   */
  dynamicList?: Observable<{title: string, data: any}[]> | undefined;
  /**
   * Extra data that needs to be sent back from the card item. Used mainly for dynamicList. This will be the item from dyanamicList return
   */
  _extra?: {title: string, data: any};
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

  constructor(private accountService: AccountService, private deviceService: DeviceService) {
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

  getLibraryActions(callback: (action: ActionItem<Library>, library: Library) => void) {
		return this.applyCallbackToList(this.libraryActions, callback);
  }

  getSeriesActions(callback: (action: ActionItem<Series>, series: Series) => void) {
		return this.applyCallbackToList(this.seriesActions, callback);
  }

  getVolumeActions(callback: (action: ActionItem<Volume>, volume: Volume) => void) {
		return this.applyCallbackToList(this.volumeActions, callback);
  }

  getChapterActions(callback: (action: ActionItem<Chapter>, chapter: Chapter) => void) {
    return this.applyCallbackToList(this.chapterActions, callback);
  }

  getCollectionTagActions(callback: (action: ActionItem<CollectionTag>, collectionTag: CollectionTag) => void) {
		return this.applyCallbackToList(this.collectionTagActions, callback);
  }

  getReadingListActions(callback: (action: ActionItem<ReadingList>, readingList: ReadingList) => void) {
    return this.applyCallbackToList(this.readingListActions, callback);
  }

  getBookmarkActions(callback: (action: ActionItem<Series>, series: Series) => void) {
    return this.applyCallbackToList(this.bookmarkActions, callback);
  }

  dummyCallback(action: ActionItem<any>, data: any) {}

  filterSendToAction(actions: Array<ActionItem<Chapter>>, chapter: Chapter) {
    // if (chapter.files.filter(f => f.format === MangaFormat.EPUB || f.format === MangaFormat.PDF).length !== chapter.files.length) {
    //   // Remove Send To as it doesn't apply
    //   return actions.filter(item => item.title !== 'Send To');
    // }
    return actions;
  }

  private _resetActions() {
    this.libraryActions = [
      {
        action: Action.Scan,
        title: 'Scan Library',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'Others',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'Refresh Covers',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'Analyze Files',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
        ],
      },
      {
        action: Action.Edit,
        title: 'Settings',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.collectionTagActions = [
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Scan,
        title: 'Scan Series',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'Add to',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
        	{
            action: Action.AddToWantToReadList,
            title: 'Add to Want to Read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'Remove from Want to Read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToReadingList,
            title: 'Add to Reading List',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToCollection,
            title: 'Add to Collection',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
        ],
      },
      {
        action: Action.Submenu,
        title: 'Send To',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            callback: this.dummyCallback,
            requiresAdmin: false,
            dynamicList: this.deviceService.devices$.pipe(map((devices: Array<Device>) => devices.map(d => {
              return {'title': d.name, 'data': d};
            }), shareReplay())),
            children: []
          }
        ],
      },
      {
        action: Action.Submenu,
        title: 'Others',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'Refresh Covers',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'Analyze Files',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Delete,
            title: 'Delete',
            callback: this.dummyCallback,
            requiresAdmin: true,
            class: 'danger',
            children: [],
          },
        ],
      },
      {
        action: Action.Download,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.volumeActions = [
      {
        action: Action.IncognitoRead,
        title: 'Read Incognito',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
			{
				action: Action.Submenu,
				title: 'Add to',
				callback: this.dummyCallback,
				requiresAdmin: false,
				children: [
					{
						action: Action.AddToReadingList,
						title: 'Add to Reading List',
						callback: this.dummyCallback,
						requiresAdmin: false,
						children: [],
					}
				]
			},
      {
        action: Action.Submenu,
        title: 'Send To',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            callback: this.dummyCallback,
            requiresAdmin: false,
            dynamicList: this.deviceService.devices$.pipe(map((devices: Array<Device>) => devices.map(d => {
              return {'title': d.name, 'data': d};
            }), shareReplay())),
            children: []
          }
        ],
      },
      {
        action: Action.Download,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'Details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.chapterActions = [
      {
        action: Action.IncognitoRead,
        title: 'Read Incognito',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'Mark as Read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'Mark as Unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
			{
				action: Action.Submenu,
				title: 'Add to',
				callback: this.dummyCallback,
				requiresAdmin: false,
				children: [
					{
						action: Action.AddToReadingList,
						title: 'Add to Reading List',
						callback: this.dummyCallback,
						requiresAdmin: false,
						children: [],
					}
				]
			},
      {
        action: Action.Submenu,
        title: 'Send To',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            callback: this.dummyCallback,
            requiresAdmin: false,
            dynamicList: this.deviceService.devices$.pipe(map((devices: Array<Device>) => devices.map(d => {
              return {'title': d.name, 'data': d};
            }), shareReplay())),
            children: []
          }
        ],
      },
      // RBS will handle rendering this, so non-admins with download are appicable
      {
        action: Action.Download,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'Details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'Edit',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'Delete',
        callback: this.dummyCallback,
        requiresAdmin: false,
        class: 'danger',
        children: [],
      },
    ];

    this.bookmarkActions = [
      {
        action: Action.ViewSeries,
        title: 'View Series',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.DownloadBookmark,
        title: 'Download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'Clear',
        callback: this.dummyCallback,
        class: 'danger',
        requiresAdmin: false,
        children: [],
      },
    ];
  }

  private applyCallback(action: ActionItem<any>, callback: (action: ActionItem<any>, data: any) => void) {
    action.callback = callback;

    if (action.children === null || action.children?.length === 0) return;

    action.children?.forEach((childAction) => {
      this.applyCallback(childAction, callback);
    });
  }

	public applyCallbackToList(list: Array<ActionItem<any>>, callback: (action: ActionItem<any>, data: any) => void): Array<ActionItem<any>> {
		const actions = list.map((a) => {
			return { ...a };
		});
		actions.forEach((action) => this.applyCallback(action, callback));
		return actions;
	}

  // Checks the whole tree for the action and returns true if it exists
  public hasAction(actions: Array<ActionItem<any>>, action: Action) {
    var actionFound = false;

    if (actions.length === 0) return actionFound;

    for (let i = 0; i < actions.length; i++)
    {
      if (actions[i].action === action) return true;
      if (this.hasAction(actions[i].children, action)) return true;
    }


    return actionFound;
  }

}
