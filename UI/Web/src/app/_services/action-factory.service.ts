import {Injectable} from '@angular/core';
import {map, Observable, shareReplay} from 'rxjs';
import {Chapter} from '../_models/chapter';
import {UserCollection} from '../_models/collection-tag';
import {Device} from '../_models/device/device';
import {Library} from '../_models/library/library';
import {ReadingList} from '../_models/reading-list';
import {Series} from '../_models/series';
import {Volume} from '../_models/volume';
import {AccountService, Role} from './account.service';
import {DeviceService} from './device.service';
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {translate} from "@jsverse/transloco";
import {Person} from "../_models/metadata/person";
import {User} from '../_models/user';

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
   * Invoke refresh covers as false to generate colorscapes
   */
  GenerateColorScape = 26,
  /**
   * Copy settings from one entity to another
   */
  CopySettings = 27,
  /**
   * Match an entity with an upstream system
   */
  Match = 28,
  /**
   * Merge two (or more?) entities
   */
  Merge = 29,
}

/**
 * Callback for an action
 */
export type ActionCallback<T> = (action: ActionItem<T>, entity: T) => void;
export type ActionShouldRenderFunc<T> = (action: ActionItem<T>, entity: T, user: User) => boolean;

export interface ActionItem<T> {
  title: string;
  description: string;
  action: Action;
  callback: ActionCallback<T>;
  /**
   * Roles required to be present for ActionItem to show. If empty, assumes anyone can see. At least one needs to apply.
   */
  requiredRoles: Role[];
  /**
   * @deprecated Use required Roles instead
   */
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
  /**
   * Will call on each action to determine if it should show for the appropriate entity based on state and user
   */
  shouldRender: ActionShouldRenderFunc<T>;
}

/**
 * Entities that can be actioned upon
 */
export type ActionableEntity = Volume | Series | Chapter | ReadingList | UserCollection | Person | Library | SideNavStream | SmartFilter | null;

@Injectable({
  providedIn: 'root',
})
export class ActionFactoryService {
  private libraryActions: Array<ActionItem<Library>> = [];
  private seriesActions: Array<ActionItem<Series>> = [];
  private volumeActions: Array<ActionItem<Volume>> = [];
  private chapterActions: Array<ActionItem<Chapter>> = [];
  private collectionTagActions: Array<ActionItem<UserCollection>> = [];
  private readingListActions: Array<ActionItem<ReadingList>> = [];
  private bookmarkActions: Array<ActionItem<Series>> = [];
  private personActions: Array<ActionItem<Person>> = [];
  private sideNavStreamActions: Array<ActionItem<SideNavStream>> = [];
  private smartFilterActions: Array<ActionItem<SmartFilter>> = [];
  private sideNavHomeActions: Array<ActionItem<void>> = [];

  constructor(private accountService: AccountService, private deviceService: DeviceService) {
    this.accountService.currentUser$.subscribe((_) => {
      this._resetActions();
    });
  }

  getLibraryActions(callback: ActionCallback<Library>, shouldRenderFunc: ActionShouldRenderFunc<Library> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.libraryActions, callback, shouldRenderFunc) as ActionItem<Library>[];
  }

  getSeriesActions(callback: ActionCallback<Series>, shouldRenderFunc: ActionShouldRenderFunc<Series> = this.basicReadRender) {
    return this.applyCallbackToList(this.seriesActions, callback, shouldRenderFunc);
  }

  getSideNavStreamActions(callback: ActionCallback<SideNavStream>, shouldRenderFunc: ActionShouldRenderFunc<SideNavStream> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.sideNavStreamActions, callback, shouldRenderFunc);
  }

  getSmartFilterActions(callback: ActionCallback<SmartFilter>, shouldRenderFunc: ActionShouldRenderFunc<SmartFilter> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.smartFilterActions, callback, shouldRenderFunc);
  }

  getVolumeActions(callback: ActionCallback<Volume>, shouldRenderFunc: ActionShouldRenderFunc<Volume> = this.basicReadRender) {
    return this.applyCallbackToList(this.volumeActions, callback, shouldRenderFunc);
  }

  getChapterActions(callback: ActionCallback<Chapter>, shouldRenderFunc: ActionShouldRenderFunc<Chapter> = this.basicReadRender) {
    return this.applyCallbackToList(this.chapterActions, callback, shouldRenderFunc);
  }

  getCollectionTagActions(callback: ActionCallback<UserCollection>, shouldRenderFunc: ActionShouldRenderFunc<UserCollection> = this.dummyShouldRender) {
    return  this.applyCallbackToList(this.collectionTagActions, callback, shouldRenderFunc);
  }

  getReadingListActions(callback: ActionCallback<ReadingList>, shouldRenderFunc: ActionShouldRenderFunc<ReadingList> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.readingListActions, callback, shouldRenderFunc);
  }

  getBookmarkActions(callback: ActionCallback<Series>, shouldRenderFunc: ActionShouldRenderFunc<Series> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.bookmarkActions, callback, shouldRenderFunc);
  }

  getPersonActions(callback: ActionCallback<Person>, shouldRenderFunc: ActionShouldRenderFunc<Person> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.personActions, callback, shouldRenderFunc);
  }

  getSideNavHomeActions(callback: ActionCallback<void>, shouldRenderFunc: ActionShouldRenderFunc<void> = this.dummyShouldRender) {
    return this.applyCallbackToList(this.sideNavHomeActions, callback, shouldRenderFunc);
  }

  dummyCallback(action: ActionItem<any>, entity: any) {}
  dummyShouldRender(action: ActionItem<any>, entity: any, user: User) {return true;}
  basicReadRender(action: ActionItem<any>, entity: any, user: User) {
    if (entity === null || entity === undefined) return true;
    if (!entity.hasOwnProperty('pagesRead') && !entity.hasOwnProperty('pages')) return true;

    switch (action.action) {
      case(Action.MarkAsRead):
        return entity.pagesRead < entity.pages;
      case(Action.MarkAsUnread):
        return entity.pagesRead !== 0;
      default:
        return true;
    }
  }

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

  getBulkLibraryActions(callback: ActionCallback<Library>, shouldRenderFunc:  ActionShouldRenderFunc<Library> = this.dummyShouldRender) {

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
      shouldRender: shouldRenderFunc,
      children: [],
      requiredRoles: [Role.Admin],
      requiresAdmin: true,
      title: 'copy-settings'
    })
    return this.applyCallbackToList(actions, callback, shouldRenderFunc) as ActionItem<Library>[];
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
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'others',
        description: '',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            description: 'analyze-files-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
        ],
      },
      {
        action: Action.Edit,
        title: 'settings',
        description: 'settings-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
    ];

    this.collectionTagActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        class: 'danger',
        children: [],
      },
      {
        action: Action.Promote,
        title: 'promote',
        description: 'promote-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Scan,
        title: 'scan-series',
        description: 'scan-series-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.AddToWantToReadList,
            title: 'add-to-want-to-read',
            description: 'add-to-want-to-read-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'remove-from-want-to-read',
            description: 'remove-to-want-to-read-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          },
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          },
          {
            action: Action.AddToCollection,
            title: 'add-to-collection',
            description: 'add-to-collection-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          },
        ],
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
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
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [],
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            description: 'analyze-files-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
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
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [Role.Download],
        children: [],
      },
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
    ];

    this.volumeActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        description: 'read-incognito-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '=',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          }
        ]
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
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
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          },
        ]
      },
      {
        action: Action.Edit,
        title: 'details',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.chapterActions = [
      {
        action: Action.IncognitoRead,
        title: 'read-incognito',
        description: 'read-incognito-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsRead,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
            children: [],
          }
        ]
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [],
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
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: true,
            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',
            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiresAdmin: false,
            requiredRoles: [Role.Download],
            children: [],
          },
        ]
      },
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.readingListActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        class: 'danger',
        children: [],
      },
      {
        action: Action.Promote,
        title: 'promote',
        description: 'promote-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.personActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-person-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Merge,
        title: 'merge',
        description: 'merge-person-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: true,
        requiredRoles: [Role.Admin],
        children: [],
      }
    ];

    this.bookmarkActions = [
      {
        action: Action.ViewSeries,
        title: 'view-series',
        description: 'view-series-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.DownloadBookmark,
        title: 'download',
        description: 'download-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'clear',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        class: 'danger',
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.sideNavStreamActions = [
      {
        action: Action.MarkAsVisible,
        title: 'mark-visible',
        description: 'mark-visible-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsInvisible,
        title: 'mark-invisible',
        description: 'mark-invisible-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.smartFilterActions = [
      {
        action: Action.Edit,
        title: 'rename',
        description: 'rename-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      },
    ];

    this.sideNavHomeActions = [
      {
        action: Action.Edit,
        title: 'reorder',
        description: '',
        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiresAdmin: false,
        requiredRoles: [],
        children: [],
      }
    ]


  }

  private applyCallback(action: ActionItem<any>, callback: ActionCallback<any>, shouldRenderFunc: ActionShouldRenderFunc<any>) {
    action.callback = callback;
    action.shouldRender = shouldRenderFunc;

    if (action.children === null || action.children?.length === 0) return;

    action.children?.forEach((childAction) => {
      this.applyCallback(childAction, callback, shouldRenderFunc);
    });
  }

  public applyCallbackToList(list: Array<ActionItem<any>>,
                             callback: ActionCallback<any>,
                             shouldRenderFunc: ActionShouldRenderFunc<any> = this.dummyShouldRender): Array<ActionItem<any>> {
    const actions = list.map((a) => {
      return { ...a };
    });
    actions.forEach((action) => this.applyCallback(action, callback, shouldRenderFunc));
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
