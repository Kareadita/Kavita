import { Injectable } from '@angular/core';
import { map, Observable, shareReplay } from 'rxjs';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { Device } from '../_models/device/device';
import { Library } from '../_models/library/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';
import { DeviceService } from './device.service';
import {SideNavStream} from "../_models/sidenav/sidenav-stream";

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
  /**
   * Import some data into Kavita
   */
  Import = 18,
  /**
   * Removes the Series from On Deck inclusion
   */
  RemoveFromOnDeck = 19,
  AddRuleGroup = 20,
  RemoveRuleGroup = 21,
  MarkAsVisible = 22,
  MarkAsInvisible = 23,
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

  sideNavStreamActions: Array<ActionItem<SideNavStream>> = [];

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

  getSideNavStreamActions(callback: (action: ActionItem<SideNavStream>, series: SideNavStream) => void) {
    return this.applyCallbackToList(this.sideNavStreamActions, callback);
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

  getMetadataFilterActions(callback: (action: ActionItem<any>, data: any) => void) {
    const actions = [
      {title: 'add-rule-group-and', action: Action.AddRuleGroup, requiresAdmin: false, children: [], callback: this.dummyCallback},
      {title: 'add-rule-group-or', action: Action.AddRuleGroup, requiresAdmin: false, children: [], callback: this.dummyCallback},
      {title: 'remove-rule-group', action: Action.RemoveRuleGroup, requiresAdmin: false, children: [], callback: this.dummyCallback},
    ];
    return this.applyCallbackToList(actions, callback);
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
        title: 'scan-library',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'others',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
        ],
      },
      {
        action: Action.Edit,
        title: 'settings',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.collectionTagActions = [
      {
        action: Action.Edit,
        title: 'edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        callback: this.dummyCallback,
        requiresAdmin: false,
        class: 'danger',
        children: [],
      },
    ];

    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Scan,
        title: 'scan-series',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
        	{
            action: Action.AddToWantToReadList,
            title: 'add-to-want-to-read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'remove-from-want-to-read',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToCollection,
            title: 'add-to-collection',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
        ],
      },
      {
        action: Action.Submenu,
        title: 'send-to',
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
        title: 'others',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            callback: this.dummyCallback,
            requiresAdmin: true,
            class: 'danger',
            children: [],
          },
        ],
      },
      {
        action: Action.Download,
        title: 'download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'edit',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.volumeActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
			{
				action: Action.Submenu,
				title: 'add-to',
				callback: this.dummyCallback,
				requiresAdmin: false,
				children: [
					{
						action: Action.AddToReadingList,
						title: 'add-to-reading-list',
						callback: this.dummyCallback,
						requiresAdmin: false,
						children: [],
					}
				]
			},
      {
        action: Action.Submenu,
        title: 'send-to',
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
        title: 'download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.chapterActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
			{
				action: Action.Submenu,
				title: 'add-to',
				callback: this.dummyCallback,
				requiresAdmin: false,
				children: [
					{
						action: Action.AddToReadingList,
						title: 'add-to-reading-list',
						callback: this.dummyCallback,
						requiresAdmin: false,
						children: [],
					}
				]
			},
      {
        action: Action.Submenu,
        title: 'send-to',
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
        title: 'download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'details',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'edit',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        callback: this.dummyCallback,
        requiresAdmin: false,
        class: 'danger',
        children: [],
      },
    ];

    this.bookmarkActions = [
      {
        action: Action.ViewSeries,
        title: 'view-series',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.DownloadBookmark,
        title: 'download',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'clear',
        callback: this.dummyCallback,
        class: 'danger',
        requiresAdmin: false,
        children: [],
      },
    ];

    this.sideNavStreamActions = [
      {
        action: Action.MarkAsVisible,
        title: 'mark-visible',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsInvisible,
        title: 'mark-invisible',
        callback: this.dummyCallback,
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
    if (actions.length === 0) return false;

    for (let i = 0; i < actions.length; i++)
    {
      if (actions[i].action === action) return true;
      if (this.hasAction(actions[i].children, action)) return true;
    }

    return false;
  }

}
