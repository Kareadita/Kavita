import {inject, Injectable} from '@angular/core';
import {NavigationStart, Router} from '@angular/router';
import {Observable, ReplaySubject, tap} from 'rxjs';
import {filter} from 'rxjs/operators';
import {ActionFactoryService} from '../_services/action-factory.service';
import {ActionService} from '../_services/action.service';
import {toSignal} from "@angular/core/rxjs-interop";
import {ActionItem, ActionShouldRenderFunc} from "../_models/actionables/action-item";
import {Action} from "../_models/actionables/action";
import {ActionResult} from "../_models/actionables/action-result";
import {LibraryType} from "../_models/library/library";
import {Volume} from "../_models/volume";
import {Chapter} from "../_models/chapter";
import {Series} from "../_models/series";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {UserCollection} from "../_models/collection-tag";
import {ReadingList} from "../_models/reading-list";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {SideNavStream} from "../_models/sidenav/sidenav-stream";

export type BulkSelectionEntityDataSource = 'volume' | 'chapter' | 'special' | 'series' | 'bookmark' | 'bookmarkData' | 'sideNavStream' | 'collection' | 'readingList' | 'annotations';

/** a closure over a signal/array, called at trigger time **/
export type BulkDataGetter<T> = () => T[];
/** provides seriesId/libraryId/libraryType for volume/chapter ops **/
export type BulkContextGetter = () => { seriesId: number; libraryId: number; libraryType?: LibraryType; };
export interface BulkResolvedEntities {
  volumes: Volume[];
  chapters: Chapter[];  // includes specials
}
/** series-detail's custom resolver for mixed volume/chapter/special selections **/
export type BulkEntityResolver = () => BulkResolvedEntities;

export type BulkCallback = ((result: ActionResult<any>) => void);

/**
 * Responsible for handling selections on cards. Can handle multiple card sources next to each other in different loops.
 * This will clear selections between pages.
 *
 * Remarks: Page which renders cards is responsible for listening for shift keydown/keyup and updating our state variable.
 */
@Injectable({
  providedIn: 'root'
})
export class BulkSelectionService {
  private actionFactory = inject(ActionFactoryService);
  private actionService = inject(ActionService);

  private dataGetters = new Map<BulkSelectionEntityDataSource, BulkDataGetter<any>>();
  private contextGetter: BulkContextGetter | null = null;
  private entityResolver: BulkEntityResolver | null = null;
  private postActionCallback: BulkCallback | null = null;
  private registeredShouldRender: ActionShouldRenderFunc<any> | null = null;


  private debug: boolean = false;
  private prevIndex: number = 0;
  private prevDataSource!: BulkSelectionEntityDataSource;
  private selectedCards: { [key: string]: {[key: number]: boolean} } = {};
  private dataSourceMax: { [key: string]: number} = {};
  public isShiftDown: boolean = false;

  private actionsSource = new ReplaySubject<ActionItem<any>[]>(1);
  public actionsSignal = toSignal(this.actionsSource);

  private selectionsSource = new ReplaySubject<number>(1);
  /**
   * Number of active selections
   */
  public readonly selections$ = this.selectionsSource.asObservable();
  public readonly selectionSignal = toSignal(this.selections$);

  constructor() {
    const router = inject(Router);

    router.events
      .pipe(filter(event => event instanceof NavigationStart))
      .subscribe(() => {
        this.deselectAll();
        this.dataSourceMax = {};
        this.prevIndex = 0;
        this.clearRegistrations();
      });
  }

  handleCardSelection(dataSource: BulkSelectionEntityDataSource, index: number, maxIndex: number, wasSelected: boolean) {
    if (this.isShiftDown) {

      if (dataSource === this.prevDataSource) {
        this.debugLog('Selecting ' + dataSource + ' cards from ' + this.prevIndex + ' to ' + index + ' as ' + !wasSelected);
        this.selectCards(dataSource, this.prevIndex, index, !wasSelected);
      } else {
        const isForwardSelection = index > this.prevIndex;

        if (isForwardSelection) {
          this.debugLog('Selecting ' + this.prevDataSource + ' cards from ' + this.prevIndex + ' to ' + this.dataSourceMax[this.prevDataSource]);
          this.selectCards(this.prevDataSource, this.prevIndex, this.dataSourceMax[this.prevDataSource], !wasSelected);
          this.debugLog('Selecting ' + dataSource + ' cards from ' + 0 + ' to ' + index);
          this.selectCards(dataSource, 0, index, !wasSelected);
        } else {
          this.debugLog('Selecting ' + this.prevDataSource + ' cards from ' + 0 + ' to ' + this.prevIndex);
          this.selectCards(this.prevDataSource, this.prevIndex, 0, !wasSelected);
          this.debugLog('Selecting ' + dataSource + ' cards from ' + index + ' to ' + maxIndex);
          this.selectCards(dataSource, index, maxIndex, !wasSelected);
        }
      }
    } else {
      this.debugLog('Selecting ' + dataSource + ' cards at ' + index);
      this.selectCards(dataSource, index, index, !wasSelected);
    }
    this.prevIndex = index;
    this.prevDataSource = dataSource;
    this.debugLog("Setting max for " + dataSource + " to " + maxIndex);
    this.dataSourceMax[dataSource] = maxIndex;
    this.actionsSource.next(this.getActions());
  }

  isCardSelected(dataSource: BulkSelectionEntityDataSource, index: number) {
    if (this.selectedCards.hasOwnProperty(dataSource) && this.selectedCards[dataSource].hasOwnProperty(index)) {
      return this.selectedCards[dataSource][index];
    }
    return false;
  }

  selectCards(dataSource: BulkSelectionEntityDataSource, from: number, to: number, value: boolean) {
    if (!this.selectedCards.hasOwnProperty(dataSource)) {
      this.selectedCards[dataSource] = {};
    }

    if (from === to) {
      this.selectedCards[dataSource][to] = value;
      this.selectionsSource.next(this.totalSelections());
      return;
    }

    if (from > to) {
      for (let i = to; i <= from; i++) {
        this.selectedCards[dataSource][i] = value;
      }
    }

    for (let i = from; i <= to; i++) {
      this.selectedCards[dataSource][i] = value;
    }
    this.selectionsSource.next(this.totalSelections());
  }

  deselectAll() {
    this.selectedCards = {};
    this.selectionsSource.next(0);
  }

  hasSelections() {
    const keys = Object.keys(this.selectedCards);
    return keys.filter(key => {
      return Object.values(this.selectedCards[key]).filter(item => item).length > 0;
    }).length > 0;
  }

  totalSelections() {
    let sum = 0;
    const keys = Object.keys(this.selectedCards);
    keys.forEach(key => {
      sum += Object.values(this.selectedCards[key]).filter(item => item).length;
    });
    return sum;
  }

  getSelectedCardsForSource(dataSource: BulkSelectionEntityDataSource) {
    if (!this.selectedCards.hasOwnProperty(dataSource)) return [];

    const ret = [];
    for(let k in this.selectedCards[dataSource]) {
      if (this.selectedCards[dataSource][k]) {
        ret.push(k);
      }
    }

    return ret;
  }

  /**
   * Returns the appropriate set of supported actions for the given mix of cards, pre-wired with callback2.
   */
  getActions(): ActionItem<any>[] {
    const allowedActions = [
      Action.AddToReadingList, Action.MarkAsRead, Action.MarkAsReadWithSession, Action.MarkAsUnread,
      Action.AddToCollection, Action.Delete, Action.AddToWantToReadList, Action.RemoveFromWantToReadList,
      Action.SetReadingProfile, Action.Download,
    ];
    const shouldRender = this.registeredShouldRender ?? this.actionFactory.dummyShouldRender;

    if (this.hasDataSource('series')) {
      const actions = this.applyFilterToList(this.actionFactory.getSeriesActions(shouldRender), allowedActions);
      return this.wireBulkCallback(actions, (action) => {
        const series = this.resolveEntities<Series>('series');
        return this.actionService.handleBulkSeriesAction(action, series);
      });
    }

    if (this.hasDataSource('bookmark')) {
      const actions = this.applyFilterToList(this.actionFactory.getBookmarkActions(() => ({seriesId: 0, libraryId: 0, seriesName: ''})), [Action.Download, Action.Delete]);
      return this.wireBulkCallback(actions, (action) => {
        const selectedSeries = this.resolveEntities<any>('bookmark');
        const seriesIds = selectedSeries.map((s: any) => s.id);
        const allBookmarks: PageBookmark[] = this.dataGetters.get('bookmarkData' as any)?.() ?? [];
        const relevantBookmarks = allBookmarks.filter(b => seriesIds.includes(b.seriesId));
        return this.actionService.handleBulkBookmarkAction(action, relevantBookmarks, seriesIds);
      });
    }

    if (this.hasDataSource('sideNavStream')) {
      const actions = this.applyFilterToList(this.actionFactory.getSideNavStreamActions(shouldRender), [Action.MarkAsInvisible, Action.MarkAsVisible]);
      return this.wireBulkCallback(actions, (action) => {
        const streams = this.resolveEntities<SideNavStream>('sideNavStream');
        return this.actionService.handleBulkSideNavStreamAction(action, streams);
      });
    }

    if (this.hasDataSource('collection')) {
      const actions = this.applyFilterToList(this.actionFactory.getCollectionTagActions(shouldRender), [Action.Promote, Action.UnPromote, Action.Delete, Action.Download]);
      return this.wireBulkCallback(actions, (action) => {
        const collections = this.resolveEntities<UserCollection>('collection');
        return this.actionService.handleBulkCollectionAction(action, collections);
      });
    }

    if (this.hasDataSource('readingList')) {
      const actions = this.applyFilterToList(this.actionFactory.getReadingListActions(shouldRender), [Action.Promote, Action.UnPromote, Action.Delete, Action.Download]);
      return this.wireBulkCallback(actions, (action) => {
        const readingLists = this.resolveEntities<ReadingList>('readingList');
        return this.actionService.handleBulkReadingListAction(action, readingLists);
      });
    }

    if (this.hasDataSource('annotations')) {
      const actions = this.actionFactory.getAnnotationActions(shouldRender);
      return this.wireBulkCallback(actions, (action) => {
        const annotations = this.resolveEntities<Annotation>('annotations');
        return this.actionService.handleBulkAnnotationAction(action, annotations);
      });
    }

    // Volume/Chapter/Special (series-detail or volume-detail)
    {
      if (this.contextGetter == null) {
        throw new Error("ContextGetter must be set for volume/chapter/special");
      }
      const ctx = this.contextGetter();
      const actions = this.applyFilterToList(this.actionFactory.getVolumeActions(ctx.seriesId, ctx.libraryId, ctx.libraryType!, shouldRender), [...allowedActions, Action.SendTo, Action.Download]);
      return this.wireBulkCallback(actions, (action) => {
        let volumes: Volume[];
        let chapters: Chapter[];

        if (this.entityResolver) {
          const resolved = this.entityResolver();
          volumes = resolved.volumes;
          chapters = resolved.chapters;
        } else {
          volumes = this.resolveEntities<Volume>('volume');
          chapters = this.resolveEntities<Chapter>('chapter');
        }

        return this.actionService.handleBulkVolumeChapterAction(action, volumes, chapters, ctx.seriesId, ctx.libraryId);
      });
    }
  }

  registerDataSource(key: BulkSelectionEntityDataSource, getter: BulkDataGetter<any>) {
    this.dataGetters.set(key, getter);
  }

  registerContext(getter: BulkContextGetter) {
    this.contextGetter = getter;
  }

  registerResolver(resolver: BulkEntityResolver) {
    this.entityResolver = resolver;
  }

  registerPostAction(callback: BulkCallback) {
    this.postActionCallback = callback;
  }

  registerShouldRender(func: ActionShouldRenderFunc<any>) {
    this.registeredShouldRender = func;
  }

  /**
   * Resolves the selected entities for a given data source by intersecting registered getter data with selected card indices.
   */
  resolveEntities<T>(dataSource: BulkSelectionEntityDataSource): T[] {
    const getter = this.dataGetters.get(dataSource);
    if (!getter) return [];
    const allItems: T[] = getter();
    const selectedIndices = this.getSelectedCardsForSource(dataSource);
    return allItems.filter((_, i) => selectedIndices.includes(i + ''));
  }

  private hasDataSource(key: BulkSelectionEntityDataSource): boolean {
    return Object.keys(this.selectedCards).includes(key);
  }

  /**
   * Wires callback on each action (and children recursively) so that triggering
   * the action resolves entities at trigger time, calls the bulk handler, then
   * auto-deselects and fires the post-action callback.
   */
  private wireBulkCallback(
    actions: ActionItem<any>[],
    handler: (action: ActionItem<any>) => Observable<ActionResult<any>>
  ): ActionItem<any>[] {
    const wire = (action: ActionItem<any>) => {
      action.callback = (act: ActionItem<any>, _entity: any) => {
        return handler(act).pipe(
          tap((act) => {
            this.deselectAll();
            this.postActionCallback?.(act);
          })
        );
      };
      if (action.children?.length) {
        action.children = action.children.map(c => ({...c}));
        action.children.forEach(c => wire(c));
      }
    };
    actions.forEach(a => wire(a));
    return actions;
  }

  private clearRegistrations() {
    this.dataGetters.clear();
    this.contextGetter = null;
    this.entityResolver = null;
    this.postActionCallback = null;
    this.registeredShouldRender = null;
  }

  private debugLog(message: string, extraData?: any) {
    if (!this.debug) return;

    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }

  private applyFilter(action: ActionItem<any>, allowedActions: Array<Action>) {
    let hasValidAction = false;

    // Check if the current action is valid or a submenu
    if (action.action === Action.Submenu || allowedActions.includes(action.action)) {
      hasValidAction = true;
    }

    // If the action has children, filter them recursively
    if (action.children && action.children.length > 0) {
      action.children = action.children.filter((childAction) => this.applyFilter(childAction, allowedActions));

      // If no valid children remain, the parent submenu should not be considered valid
      if (action.children.length === 0 && action.action === Action.Submenu) {
        hasValidAction = false;
      }
    }

    // Return whether this action or its children are valid
    return hasValidAction;
  }

	private applyFilterToList(list: Array<ActionItem<any>>, allowedActions: Array<Action>): Array<ActionItem<any>> {
		const actions = list.map((a) => {
			return { ...a };
		});
    return actions.filter(action => this.applyFilter(action, allowedActions));
	}
}
