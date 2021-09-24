import { Injectable } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { Action, ActionFactoryService } from '../_services/action-factory.service';

type DataSource = 'volume' | 'chapter' | 'special' | 'series';

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

  private prevIndex: number = 0;
  private prevDataSource!: DataSource;
  private selectedCards: { [key: string]: {[key: number]: boolean} } = {};
  public isShiftDown: boolean = false;

  constructor(private router: Router, private actionFactory: ActionFactoryService) {
    router.events
      .pipe(filter(event => event instanceof NavigationStart))
      .subscribe((event) => {
        this.deselectAll();
      });
  }

  handleCardSelection(dataSource: DataSource, index: number, maxIndex: number, wasSelected: boolean) {
    if (this.isShiftDown) {

      if (dataSource === this.prevDataSource) {
        console.log('Selecting ' + dataSource + ' cards from ' + this.prevIndex + ' to ' + index);
        this.selectCards(dataSource, this.prevIndex, index, !wasSelected);  
      } else {
        const isForwardSelection = index < this.prevIndex;

        if (isForwardSelection) {
          console.log('Selecting ' + this.prevDataSource + ' cards from ' + this.prevIndex + ' to ' + maxIndex);
          this.selectCards(this.prevDataSource, this.prevIndex, maxIndex, !wasSelected);  
          console.log('Selecting ' + dataSource + ' cards from ' + 0 + ' to ' + maxIndex);
          this.selectCards(dataSource, 0, index, !wasSelected);
        } else {
          console.log('Selecting ' + this.prevDataSource + ' cards from ' + 0 + ' to ' + this.prevIndex);
          this.selectCards(this.prevDataSource, this.prevIndex, 0, !wasSelected);  
          console.log('Selecting ' + dataSource + ' cards from ' + index + ' to ' + maxIndex);
          this.selectCards(dataSource, index, maxIndex, !wasSelected);
        }
      }
    } else {
      console.log('Selecting ' + dataSource + ' cards at ' + index);
      this.selectCards(dataSource, index, index, !wasSelected);
    }
    this.prevIndex = index;
    this.prevDataSource = dataSource;
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
  }

  deselectAll() {
    this.selectedCards = {};
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

  getActions(callback: (action: Action, data: any) => void) {
    // checks if series is present. If so, returns only series actions
    // else returns volume/chapter items
    const allowedActions = [Action.MarkAsRead, Action.MarkAsUnread, Action.AddToReadingList];
    if (Object.keys(this.selectedCards).filter(item => item === 'series').length > 0) {
      return this.actionFactory.getSeriesActions(callback).filter(item => allowedActions.includes(item.action));
    }

    return this.actionFactory.getVolumeActions(callback).filter(item => allowedActions.includes(item.action));
  }
}
