import {effect, inject, Injectable} from '@angular/core';
import {EMPTY, map, of, shareReplay, switchMap} from 'rxjs';
import {Chapter} from '../_models/chapter';
import {UserCollection} from '../_models/collection-tag';
import {Device} from '../_models/device/device';
import {Library, LibraryType} from '../_models/library/library';
import {ReadingList} from '../_models/reading-list/reading-list';
import {Series} from '../_models/series';
import {Volume} from '../_models/volume';
import {AccountService, Role} from './account.service';
import {DeviceService} from './device.service';
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {translate} from "@jsverse/transloco";
import {Person} from "../_models/metadata/person";
import {User} from '../_models/user/user';
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {ClientDevice} from "../_models/client-device";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {ActionService} from "./action.service";
import {ActionItem, ActionShouldRenderFunc} from "../_models/actionables/action-item";
import {Action} from "../_models/actionables/action";
import {ActionResultCallback} from "../_models/actionables/action-result";
import {SettingsService} from "../admin/settings.service";


/**
 * Entities that can be actioned upon
 */
export type ActionableEntity = Volume | Series | Chapter | ReadingList | UserCollection | Person | Library | SideNavStream | SmartFilter | ClientDevice | PageBookmark | null;

@Injectable({
  providedIn: 'root',
})
export class ActionFactoryService {
  private accountService = inject(AccountService);
  private deviceService = inject(DeviceService);
  private actionService = inject(ActionService);
  private settingsService = inject(SettingsService);

  private libraryActions: Array<ActionItem<Library>> = [];
  private seriesActions: Array<ActionItem<Series>> = [];
  private volumeActions: Array<ActionItem<Volume>> = [];
  private chapterActions: Array<ActionItem<Chapter>> = [];
  private collectionTagActions: Array<ActionItem<UserCollection>> = [];
  private readingListActions: Array<ActionItem<ReadingList>> = [];
  private bookmarkActions: Array<ActionItem<PageBookmark>> = [];
  private personActions: Array<ActionItem<Person>> = [];
  private sideNavStreamActions: Array<ActionItem<SideNavStream>> = [];
  private smartFilterActions: Array<ActionItem<SmartFilter>> = [];
  private sideNavHomeActions: Array<ActionItem<{}>> = [];
  private annotationActions: Array<ActionItem<Annotation>> = [];
  private clientDeviceActions: Array<ActionItem<ClientDevice>> = [];

  constructor() {
    this._resetActions();
    effect(() => {
      this.accountService.currentUser();
      this._resetActions();
    });
  }

  getLibraryActions(shouldRenderFunc: ActionShouldRenderFunc<Library> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.libraryActions,
      (action, entity) => this.actionService.handleLibraryAction(action, entity),
      shouldRenderFunc
    );
  }

  getSeriesActions(shouldRenderFunc: ActionShouldRenderFunc<Series> = this.basicReadRender, onDeck: boolean = false) {
    return this.applyCallbackToList(
      this.seriesActions,
      (action, entity) => this.actionService.handleSeriesAction(action, entity),
      (action, entity, user) => {
        if (action.action === Action.RemoveFromOnDeck) return onDeck

        return shouldRenderFunc(action, entity, user);
      }
    );
  }

  getVolumeActions(seriesId: number, libraryId: number, libraryType: LibraryType, shouldRenderFunc: ActionShouldRenderFunc<Volume> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.volumeActions,
      (action, entity) => this.actionService.handleVolumeAction(action, entity, seriesId, libraryId, libraryType),
      shouldRenderFunc
    );
  }

  getChapterActions(seriesId: number, libraryId: number, libraryType: LibraryType, shouldRenderFunc: ActionShouldRenderFunc<Chapter> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.chapterActions,
      (action, entity) => this.actionService.handleChapterAction(action, entity, seriesId, libraryId, libraryType),
      shouldRenderFunc
    );
  }

  getBookmarkActions(contextFunc: () => {seriesId: number, libraryId: number, seriesName: string}, shouldRenderFunc: ActionShouldRenderFunc<PageBookmark> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.bookmarkActions,
      (action, entity) => this.actionService.handleBookmarkAction(action, entity, contextFunc),
      shouldRenderFunc
    );
  }

  getReadingListActions(shouldRenderFunc: ActionShouldRenderFunc<ReadingList> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.readingListActions,
      (action, entity) => this.actionService.handleReadingListAction(action, entity),
      shouldRenderFunc
    );
  }

  getCollectionTagActions(shouldRenderFunc: ActionShouldRenderFunc<UserCollection> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.collectionTagActions,
      (action, entity) => this.actionService.handleCollectionAction(action, entity),
      shouldRenderFunc
    );
  }

  getAnnotationActions(shouldRenderFunc: ActionShouldRenderFunc<Annotation> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.annotationActions,
      (action, entity) => this.actionService.handleAnnotationAction(action, entity),
      shouldRenderFunc
    );
  }

  getClientDeviceActions(shouldRenderFunc: ActionShouldRenderFunc<ClientDevice> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.clientDeviceActions,
      (action, entity) => this.actionService.handleClientDeviceAction(action, entity),
      shouldRenderFunc
    );
  }

  getPersonActions(shouldRenderFunc: ActionShouldRenderFunc<Person> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.personActions,
      (action, entity) => this.actionService.handlePersonAction(action, entity),
      shouldRenderFunc
    );
  }

  getSmartFilterActions(allFilters: SmartFilter[], shouldRenderFunc: ActionShouldRenderFunc<SmartFilter> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.smartFilterActions,
      (action, entity) => this.actionService.handleSmartFilterAction(action, entity, allFilters),
      shouldRenderFunc
    );
  }


  getSideNavStreamActions(shouldRenderFunc: ActionShouldRenderFunc<SideNavStream> = this.basicReadRender) {
    return this.applyCallbackToList(
      this.sideNavStreamActions,
      (action, entity) => this.actionService.handleSideNavStreamAction(action, entity),
      shouldRenderFunc
    );
  }

  getSideNavHomeActions(shouldRenderFunc: ActionShouldRenderFunc<{}> = this.basicReadRender) {
    // If the caller doesn't pass a render function, assume that readonly users cannot perform actions
    const renderFunc = shouldRenderFunc === this.basicReadRender
      ? (action: ActionItem<any>, entity: any, user: User) => !this.accountService.hasReadOnlyRole()
      : shouldRenderFunc;

    return this.applyCallbackToList(
      this.sideNavHomeActions,
      (action, entity) => this.actionService.handleSideNavHomeStream(action, entity),
      renderFunc
    );
  }

  getBulkLibraryActions(shouldRenderFunc: ActionShouldRenderFunc<Library> = this.basicReadRender) {

    const filteredActions = this.flattenActions<Library>(this.libraryActions).filter(a => {
      return [Action.Delete, Action.GenerateColorScape, Action.RefreshMetadata, Action.CopySettings].includes(a.action);
    });

    filteredActions.push({
      _extra: undefined,
      class: undefined,
      description: '',
      dynamicList: undefined,
      action: Action.CopySettings,
      callback: this.dummyCallback,
      shouldRender: shouldRenderFunc,
      children: [],
      requiredRoles: [Role.Admin],
      title: 'copy-settings'
    });

    return this.applyCallbackToList(
      filteredActions,
      (action, entity) => this.actionService.handleBulkLibraryAction(action, entity),
      shouldRenderFunc
    );
  }

  dummyCallback(action: ActionItem<any>, entity: any) { return EMPTY; }
  dummyShouldRender(action: ActionItem<any>, entity: any, user: User) {return true;}
  basicReadRender(action: ActionItem<any>, entity: any, user: User) {
    if (entity === null || entity === undefined) return true;
    if (!entity.hasOwnProperty('pagesRead') && !entity.hasOwnProperty('pages')) return true;
    switch (action.action) {
      case(Action.MarkAsRead):
      case(Action.MarkAsReadWithSession):
        return entity.pagesRead < entity.pages;
      case(Action.MarkAsUnread):
        return entity.pagesRead !== 0;
      default:
        return true;
    }
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

  private sendToChildren() {
    return this.settingsService.isEmailSetup().pipe(
      switchMap(isSetup => {
        if (!isSetup) return of([]);

        return this.deviceService.devices$;
      }),
      map((devices: Array<Device>) => devices.map(d => {
        return {'title': d.name, 'data': d};
      })),
      shareReplay(),
    )
  }


  private _resetActions() {
    this.libraryActions = [
      {
        action: Action.Scan,
        title: 'scan-library',
        description: 'scan-library-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'reading-profiles',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [
          {
            action: Action.SetReadingProfile,
            title: 'set-reading-profile',
            description: 'set-reading-profile-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiredRoles: [],
            children: [],
          },
          {
            action: Action.ClearReadingProfile,
            title: 'clear-reading-profile',
            description: 'clear-reading-profile-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
        ],
      },
      {
        action: Action.Submenu,
        title: 'others',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Admin],
        children: [
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Download],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [],
      },
    ];

    this.seriesActions = [
      {
        action: Action.Submenu,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.MarkAsRead,
            title: 'mark-as-read',
            description: 'mark-as-read-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.MarkAsReadWithSession,
            title: 'mark-as-read-with-session',
            description: 'mark-as-read-with-session-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          }
        ],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Scan,
        title: 'scan-series',
        description: 'scan-series-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.AddToWantToReadList,
            title: 'add-to-want-to-read',
            description: 'add-to-want-to-read-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.RemoveFromWantToReadList,
            title: 'remove-from-want-to-read',
            description: 'remove-from-want-to-read-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.AddToCollection,
            title: 'add-to-collection',
            description: 'add-to-collection-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          }
        ],
      },
      {
        action: Action.Submenu,
        title: 'send-to',
        description: 'send-to-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            dynamicList: this.sendToChildren(),
            children: []
          }
        ],
      },
      {
        action: Action.Submenu,
        title: 'reading-profiles',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.SetReadingProfile,
            title: 'set-reading-profile',
            description: 'set-reading-profile-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.ClearReadingProfile,
            title: 'clear-reading-profile',
            description: 'clear-reading-profile-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
        ],
      },
      {
        action: Action.Submenu,
        title: 'others',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.RemoveFromOnDeck,
            title: 'remove-from-on-deck',
            description: 'remove-from-on-deck-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.RefreshMetadata,
            title: 'refresh-covers',
            description: 'refresh-covers-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.GenerateColorScape,
            title: 'generate-colorscape',
            description: 'generate-colorscape-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.AnalyzeFiles,
            title: 'analyze-files',
            description: 'analyze-files-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Download],
        children: [],
      },
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.MarkAsRead,
            title: 'mark-as-read',
            description: 'mark-as-read-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.MarkAsReadWithSession,
            title: 'mark-as-read-with-session',
            description: 'mark-as-read-with-session-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          }
        ],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '=',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            dynamicList: this.sendToChildren(),
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

        requiredRoles: [],
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'mark-as-read',
        description: 'mark-as-read-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.MarkAsRead,
            title: 'mark-as-read',
            description: 'mark-as-read-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          },
          {
            action: Action.MarkAsReadWithSession,
            title: 'mark-as-read-with-session',
            description: 'mark-as-read-with-session-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            children: [],
          }
        ],
      },
      {
        action: Action.MarkAsUnread,
        title: 'mark-as-unread',
        description: 'mark-as-unread-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'add-to',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.AddToReadingList,
            title: 'add-to-reading-list',
            description: 'add-to-reading-list-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [
          {
            action: Action.SendTo,
            title: '',
            description: '',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [],
            dynamicList: this.sendToChildren(),
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

        requiredRoles: [],
        children: [
          {
            action: Action.Delete,
            title: 'delete',
            description: 'delete-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

            requiredRoles: [Role.Admin],
            children: [],
          },
          {
            action: Action.Download,
            title: 'download',
            description: 'download-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Download],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.UnPromote,
        title: 'unpromote',
        description: 'unpromote-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Submenu,
        title: 'export',
        description: 'export-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [],
        children: [
          {
            action: Action.ExportAsV1,
            title: 'export-v1',
            description: 'export-v1-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiredRoles: [],
            children: [],
          },
          {
            action: Action.ExportAsV2,
            title: 'export-v2',
            description: 'export-v2-tooltip',

            callback: this.dummyCallback,
            shouldRender: this.dummyShouldRender,
            requiredRoles: [],
            children: [],
          }
        ],
      }
    ];

    this.personActions = [
      {
        action: Action.Edit,
        title: 'edit',
        description: 'edit-person-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

        requiredRoles: [Role.Admin],
        children: [],
      },
      {
        action: Action.Merge,
        title: 'merge',
        description: 'merge-person-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Download,
        title: 'download',
        description: 'download-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.MarkAsInvisible,
        title: 'mark-invisible',
        description: 'mark-invisible-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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

        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: 'delete-tooltip',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,

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
        requiredRoles: [],
        children: [],
      }
    ];

    this.annotationActions = [
      {
        action: Action.Delete,
        title: 'delete',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Export,
        title: 'export',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Like,
        title: 'like',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.UnLike,
        title: 'unlike',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      },
    ];

    this.clientDeviceActions = [
      {
        action: Action.Edit,
        title: 'edit-device-name',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      },
      {
        action: Action.Delete,
        title: 'delete',
        description: '',

        callback: this.dummyCallback,
        shouldRender: this.dummyShouldRender,
        requiredRoles: [],
        children: [],
      }
    ];


  }

  private applyCallback(action: ActionItem<any>, callback: ActionResultCallback<any>, shouldRenderFunc: ActionShouldRenderFunc<any>) {
    action.callback = callback;
    action.shouldRender = shouldRenderFunc;

    if (action.children === null || action.children?.length === 0) return;

    // Ensure action children are a copy of the parent (since parent does a shallow mapping)
    action.children = action.children.map(d => { return {...d}; });

    action.children.forEach((childAction) => {
      this.applyCallback(childAction, callback, shouldRenderFunc);
    });
  }

  public applyCallbackToList<T>(list: Array<ActionItem<T>>,
                             callback: ActionResultCallback<any>,
                             shouldRenderFunc: ActionShouldRenderFunc<T> = this.dummyShouldRender): Array<ActionItem<T>> {
    // Create a clone of the list to ensure we aren't affecting the default state
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
