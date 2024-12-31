import { Injectable } from '@angular/core';
import { map, Observable, shareReplay } from 'rxjs';
import { Chapter } from '../_models/chapter';
import {UserCollection} from '../_models/collection-tag';
import { Device } from '../_models/device/device';
import { Library } from '../_models/library/library';
import { ReadingList } from '../_models/reading-list';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';
import { DeviceService } from './device.service';
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {translate} from "@jsverse/transloco";
import {Person} from "../_models/metadata/person";

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
  /**
   * Promotes the underlying item (Reading List, Collection)
   */
  Promote = 24,
  UnPromote = 25,
  /**
   * Invoke a refresh covers as false to generate colorscapes
   */
  GenerateColorScape = 26,
  /**
   * Copy settings from one entity to another
   */
  CopySettings = 27,
  /**
   * Match an entity with an upstream system
   */
  Match = 28
}

/**
 * Callback for an action
 */
export type ActionCallback<T> = (action: ActionItem<T>, data: T) => void;
export type ActionAllowedCallback<T> = (action: ActionItem<T>) => boolean;

export interface ActionItem<T> {
  title: string;
  description: string;
  action: Action;
  callback: ActionCallback<T>;
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

  collectionTagActions: Array<ActionItem<UserCollection>> = [];

  readingListActions: Array<ActionItem<ReadingList>> = [];

  bookmarkActions: Array<ActionItem<Series>> = [];

  private personActions: Array<ActionItem<Person>> = [];

  sideNavStreamActions: Array<ActionItem<SideNavStream>> = [];
  smartFilterActions: Array<ActionItem<SmartFilter>> = [];

  isAdmin = false;


  constructor(private accountService: AccountService, private deviceService: DeviceService) {
    this.accountService.currentUser$.subscribe((user) => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      } else {
        this._resetActions();
        return; // If user is logged out, we don't need to do anything
      }

      this._resetActions();
    });
  }

  getLibraryActions(callback: ActionCallback<Library>) {
    return this.applyCallbackToList(this.libraryActions, callback);
  }

  getSeriesActions(callback: ActionCallback<Series>) {
    return this.applyCallbackToList(this.seriesActions, callback);
  }

  getSideNavStreamActions(callback: ActionCallback<SideNavStream>) {
    return this.applyCallbackToList(this.sideNavStreamActions, callback);
  }

  getSmartFilterActions(callback: ActionCallback<SmartFilter>) {
    return this.applyCallbackToList(this.smartFilterActions, callback);
  }

  getVolumeActions(callback: ActionCallback<Volume>) {
    return this.applyCallbackToList(this.volumeActions, callback);
  }

  getChapterActions(callback: ActionCallback<Chapter>) {
    return this.applyCallbackToList(this.chapterActions, callback);
  }

  getCollectionTagActions(callback: ActionCallback<UserCollection>) {
    return  this.applyCallbackToList(this.collectionTagActions, callback);
  }

  getReadingListActions(callback: ActionCallback<ReadingList>) {
    return this.applyCallbackToList(this.readingListActions, callback);
  }

  getBookmarkActions(callback: ActionCallback<Series>) {
    return this.applyCallbackToList(this.bookmarkActions, callback);
  }

  getPersonActions(callback: ActionCallback<Person>) {
    return this.applyCallbackToList(this.personActions, callback);
  }

  dummyCallback(action: ActionItem<any>, data: any) {}

  filterSendToAction(actions: Array<ActionItem<Chapter>>, chapter: Chapter) {
    // if (chapter.files.filter(f => f.format === MangaFormat.EPUB || f.format === MangaFormat.PDF).length !== chapter.files.length) {
    //   // Remove Send To as it doesn't apply
    //   return actions.filter(item => item.title !== 'Send To');
    // }
    return actions;
  }

  getActionablesForSettingsPage(actions: Array<ActionItem<any>>, blacklist: Array<Action> = []) {
    const tasks = [];

    let actionItem;
    for (let parent of actions) {
      if (parent.action === Action.SendTo) continue;

      if (parent.children.length === 0) {
        actionItem = {...parent};
        actionItem.title = translate('actionable.' + actionItem.title);
        if (actionItem.description !== '') {
          actionItem.description = translate('actionable.' + actionItem.description);
        }

        tasks.push(actionItem);
        continue;
      }

      for (let child of parent.children) {
        if (child.action === Action.SendTo) continue;
        actionItem = {...child};
        actionItem.title = translate('actionable.' + actionItem.title);
        if (actionItem.description !== '') {
          actionItem.description = translate('actionable.' + actionItem.description);
        }
        tasks.push(actionItem);
      }
    }

    // Filter out tasks that don't make sense
    return tasks.filter(t => !blacklist.includes(t.action));
  }

  getBulkLibraryActions(callback: ActionCallback<Library>) {

    // Scan is currently not supported due to the backend not being able to handle it yet
    const actions = this.flattenActions<Library>(this.libraryActions).filter(a => {
      return [Action.Delete, Action.GenerateColorScape, Action.AnalyzeFiles, Action.RefreshMetadata, Action.CopySettings].includes(a.action);
    });

    actions.push({
      _extra: undefined,
      class: undefined,
      description: '',
      dynamicList: undefined,
      action: Action.CopySettings,
      callback: this.dummyCallback,
      children: [],
      requiresAdmin: true,
      title: 'copy-settings'
    })
    return this.applyCallbackToList(actions, callback);
  }

  flattenActions<T>(actions: Array<ActionItem<T>>): Array<ActionItem<T>> {
    return actions.reduce<Array<ActionItem<T>>>((flatArray, action) => {
      if (action.action !== Action.Submenu) {
        flatArray.push(action);
      }

      // Recursively flatten the children, if any
      if (action.children && action.children.length > 0) {
        flatArray.push(...this.flattenActions<T>(action.children));
      }

      return flatArray;
    }, [] as Array<ActionItem<T>>); // Explicitly defining the type of flatArray
  }


  private _resetActions() {
    this.libraryActions = [
      {
        action: Action.Scan,
        title: 'scan-library',
        description: 'scan-library-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'others',
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            description: 'analyze-files-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
        ],
      },
      {
        action: Action.Edit,
        title: 'settings',
        description: 'settings-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.collectionTagActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        class: 'danger',
        children: [],
      },
      {
        action: Action.Promote,
        title: 'promote',
        description: 'promote-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Scan,
        title: 'scan-series',
        description: 'scan-series-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.AddToWantToReadList,
            title: 'add-to-want-to-read',
            description: 'add-to-want-to-read-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'remove-from-want-to-read',
            description: 'remove-to-want-to-read-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
          {
            action: Action.AddToCollection,
            title: 'add-to-collection',
            description: 'add-to-collection-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },

          // {
          //   action: Action.AddToScrobbleHold,
          //   title: 'add-to-scrobble-hold',
          //   description: 'add-to-scrobble-hold-tooltip',
          //   callback: this.dummyCallback,
          //   requiresAdmin: true,
          //   children: [],
          // },
          // {
          //   action: Action.RemoveFromScrobbleHold,
          //   title: 'remove-from-scrobble-hold',
          //   description: 'remove-from-scrobble-hold-tooltip',
          //   callback: this.dummyCallback,
          //   requiresAdmin: true,
          //   children: [],
          // },
        ],
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
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
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            description: 'analyze-files-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            class: 'danger',
            children: [],
          },
        ],
      },
      {
        action: Action.Match,
        title: 'match',
        description: 'match-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      },
    ];

    this.volumeActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        description: 'read-incognito-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '=',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          }
        ]
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
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
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
        ]
      },
      {
        action: Action.Edit,
        title: 'details',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.chapterActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        description: 'read-incognito-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          }
        ]
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
            callback: this.dummyCallback,
            requiresAdmin: false,
            dynamicList: this.deviceService.devices$.pipe(map((devices: Array<Device>) => devices.map(d => {
              return {'title': d.name, 'data': d};
            }), shareReplay())),
            children: []
          }
        ],
      },
      // RBS will handle rendering this, so non-admins with download are applicable
      {
        action: Action.Submenu,
        title: 'others',
        description: '',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: true,
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',
            callback: this.dummyCallback,
            requiresAdmin: false,
            children: [],
          },
        ]
      },
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        class: 'danger',
        children: [],
      },
      {
        action: Action.Promote,
        title: 'promote',
        description: 'promote-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.personActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-person-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: true,
        children: [],
      }
    ];

    this.bookmarkActions = [
      {
        action: Action.ViewSeries,
        title: 'view-series',
        description: 'view-series-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.DownloadBookmark,
        title: 'download',
        description: 'download-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.Delete,
        title: 'clear',
        description: 'delete-tooltip',
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
        description: 'mark-visible-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
      {
        action: Action.MarkAsInvisible,
        title: 'mark-invisible',
        description: 'mark-invisible-tooltip',
        callback: this.dummyCallback,
        requiresAdmin: false,
        children: [],
      },
    ];

    this.smartFilterActions = [
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
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
