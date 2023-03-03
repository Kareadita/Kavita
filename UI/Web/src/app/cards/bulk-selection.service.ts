import { Injectable } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { ReplaySubject } from 'rxjs';
import { filter } from 'rxjs/operators';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';

type DataSource = 'volume' | 'chapter' | 'special' | 'series' | 'bookmark';

/**
 * Responsible for handling selections on cards. Can handle multiple card sources next to each other in different loops.
 * This will clear selections between pages.
 * 
 * Remakrs: Page which renders cards is responsible for listening for shift keydown/keyup and updating our state variable.
 */
@Injectable({
  providedIn: 'root'
})
export class BulkSelectionService {

  private debug: boolean = false;
  private prevIndex: number = 0;
  private prevDataSource!: DataSource;
  private selectedCards: { [key: string]: {[key: number]: boolean} } = {};
  private dataSourceMax: { [key: string]: number} = {};
  public isShiftDown: boolean = false;

  private actionsSource = new ReplaySubject<ActionItem<any>[]>(1);
  public actions$ = this.actionsSource.asObservable();

  private selectionsSource = new ReplaySubject<number>(1);
  /**
   * Number of active selections
   */
  public selections$ = this.selectionsSource.asObservable();

  constructor(router: Router, private actionFactory: ActionFactoryService) {
    router.events
      .pipe(filter(event => event instanceof NavigationStart))
      .subscribe(() => {
        this.deselectAll();
        this.dataSourceMax = {};
        this.prevIndex = 0;
      });

  }

  handleCardSelection(dataSource: DataSource, index: number, maxIndex: number, wasSelected: boolean) {
    if (this.isShiftDown) {

      if (dataSource === this.prevDataSource) {
        this.debugLog('Selecting ' + dataSource + ' cards from ' + this.prevIndex + ' to ' + index);
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
    this.dataSourceMax[dataSource] = maxIndex;
    this.actionsSource.next(this.getActions(() => {}));
  }

  isCardSelected(dataSource: DataSource, index: number) {
    if (this.selectedCards.hasOwnProperty(dataSource) && this.selectedCards[dataSource].hasOwnProperty(index)) {
      return this.selectedCards[dataSource][index];
    }
    return false;
  }

  selectCards(dataSource: DataSource, from: number, to: number, value: boolean) {
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

  getSelectedCardsForSource(dataSource: DataSource) {
    if (!this.selectedCards.hasOwnProperty(dataSource)) return [];

    let ret = [];
    for(let k in this.selectedCards[dataSource]) {
      if (this.selectedCards[dataSource][k]) {
        ret.push(k);
      }
    }
    
    return ret;
  }

  getActions(callback: (action: ActionItem<any>, data: any) => void) {
    // checks if series is present. If so, returns only series actions
    // else returns volume/chapter items
    const allowedActions = [Action.AddToReadingList, Action.MarkAsRead, Action.MarkAsUnread, Action.AddToCollection, 
      Action.Delete, Action.AddToWantToReadList, Action.RemoveFromWantToReadList];
    if (Object.keys(this.selectedCards).filter(item => item === 'series').length > 0) {
      return this.applyFilterToList(this.actionFactory.getSeriesActions(callback), allowedActions);
    }

    if (Object.keys(this.selectedCards).filter(item => item === 'bookmark').length > 0) {
      return this.actionFactory.getBookmarkActions(callback);
    }

    return this.applyFilterToList(this.actionFactory.getVolumeActions(callback), allowedActions);
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
    
    var ret = false;
    if (action.action === Action.Submenu || allowedActions.includes(action.action)) {
      // Do something
      ret = true;
    }

    if (action.children === null || action.children?.length === 0) return ret;

    action.children = action.children.filter((childAction) => this.applyFilter(childAction, allowedActions));
    
    return ret;
  }

	private applyFilterToList(list: Array<ActionItem<any>>, allowedActions: Array<Action>): Array<ActionItem<any>> {
		const actions = list.map((a) => {
			return { ...a };
		});
    return actions.filter(action => this.applyFilter(action, allowedActions));
	}
}
