import {SelectionCompareFn} from "./typeahead-settings";

/**
 * SelectionModel<T> is used for keeping track of multiple selections. Simple interface with ability to toggle.
 * @param selectedState Optional state to set selectedOptions to. If not passed, defaults to false.
 * @param selectedOptions Optional data elements to inform the SelectionModel of. If not passed, as toggle() occur, items are tracked.
 * @param propAccessor Optional string that points to a unique field within the T type. Used for quickly looking up.
 */
export class SelectionModel<T> {
  _data!: Array<{value: T, selected: boolean}>;
  _propAccessor: string = '';

  constructor(selectedState: boolean = false, selectedOptions: Array<T> = [], propAccessor: string = '') {
    this._data = [];

    if (propAccessor != undefined || propAccessor !== '') {
      this._propAccessor = propAccessor;
    }

    selectedOptions.forEach(d => {
      this._data.push({value: d, selected: selectedState});
    });
  }

  /**
   * Will toggle if the data item is selected or not. If data option is not tracked, will add it and set state to true.
   * @param data Item to toggle
   * @param selectedState Force the state
   * @param compareFn An optional function to use for the lookup, else will use shallowEqual implementation
   */
  toggle(data: T, selectedState?: boolean, compareFn?: SelectionCompareFn<T>) {
    const lookupMethod = compareFn || this.defaultEqual;

    const dataItem = this._data.filter(d => lookupMethod(d.value, data));
    if (dataItem.length > 0) {
      if (selectedState != undefined) {
        dataItem[0].selected = selectedState;
      } else {
        dataItem[0].selected = !dataItem[0].selected;
      }
    } else {
      this._data.push({value: data, selected: (selectedState === undefined ? true : selectedState)});
    }
  }


  /**
   * Is the passed item selected
   * @param data item to check against
   * @param compareFn optional method to use to perform comparisons
   * @returns boolean
   */
  isSelected(data: T, compareFn?: SelectionCompareFn<T>): boolean {
    const lookupMethod = compareFn || this.defaultEqual;

    const dataItem = this._data.filter(d => lookupMethod(d.value, data));

    if (dataItem.length > 0) {
      return dataItem[0].selected;
    }
    return false;
  }

  /**
   *
   * @returns If some of the items are selected, but not all
   */
  hasSomeSelected(): boolean {
    const selectedCount = this._data.filter(d => d.selected).length;
    return (selectedCount !== this._data.length && selectedCount !== 0)
  }

  /**
   *
   * @returns All Selected items
   */
  selected(): Array<T> {
    return this._data.filter(d => d.selected).map(d => d.value);
  }

  /**
   *
   * @returns All Non-Selected items
   */
  unselected(): Array<T> {
    return this._data.filter(d => !d.selected).map(d => d.value);
  }

  /**
   *
   * @returns Last element added/tracked or undefined if nothing is tracked
   */
  peek(): T | undefined {
    if (this._data.length > 0) {
      return this._data[this._data.length - 1].value;
    }

    return undefined;
  }

  private defaultEqual = (a: T, b: T): boolean => {
    if (typeof a === 'object' && a !== null && typeof b === 'object' && b !== null) {
      return this.shallowEqual(a, b);
    }
    return a === b;
  }

  private shallowEqual(a: object, b: object): boolean {
    for (const key in a) {
      if (!(key in b) || (a as any)[key] !== (b as any)[key]) {
        return false;
      }
    }
    for (const key in b) {
      if (!(key in a)) {
        return false;
      }
    }
    return true;
  }
}
