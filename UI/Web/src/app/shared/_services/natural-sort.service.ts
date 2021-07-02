import { Injectable, OnDestroy } from '@angular/core';

/**
 * Soley repsonsible for performing a "natural" sort. This is the UI counterpart to the BE NaturalSortComparer.
 */
@Injectable({
  providedIn: 'root',
})
export class NaturalSortService implements OnDestroy {

  private _table: Map<string, string[]> = new Map<string, string[]>();

  ngOnDestroy(): void {
    this._table = new Map<string, string[]>();
  }

  private partCompare(left: string, right: string) {

    let x = parseInt(left, 10);
    if (typeof x !== 'number') {
      return left > right;
    }

    let y = parseInt(right, 10);
    if (typeof y !== 'number') {
      return left > right;
    }

    if (x === null || x === undefined) {
      return 1;
    }
    if (y === null || y === undefined) {
      return -1;
    }

    return x > y;
  }

  compare(x: string, y: string, isAscending: boolean) {
    if (x == y) return 0;

    let x1 = this._table.get(x);
    if (x1 === undefined) {
      x1 = x.split(/([0-9]+)/);
      this._table.set(x, x1);
    }

    let y1 = this._table.get(y);
    if (y1 === undefined) {
      y1 = y.split(/([0-9]+)/);
      this._table.set(y, y1);
    }

    let returnVal = 0;
    for (var i = 0; i < x1.length && i < y1.length; i++) {
        if (x1[i] == y1[i]) continue;
        returnVal = (this.partCompare(x1[i], y1[i]) ? 1 : 0);
        return isAscending ? returnVal : -returnVal;
    }

    if (y1.length > x1.length) {
        returnVal = 1;
    } else if (x1.length > y1.length) { 
        returnVal = -1; 
    }

    return isAscending ? returnVal : -returnVal;
  }
}
