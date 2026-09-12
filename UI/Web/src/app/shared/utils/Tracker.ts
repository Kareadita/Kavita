import {computed, Signal, signal} from "@angular/core";

interface IHasId {
  id: number;
}

/**
 * Tracker<T> allows tracking selected state of items in a reactive (signals) context
 */
export class Tracker<T> {

  private data = signal<{value: T, selected: boolean}[]>([]);
  private readonly compareFn: (a: T, b: T) => boolean;

  constructor(compareFn: (a: T, b: T) => boolean) {
    this.compareFn = compareFn;
  }

  public static IdTracker<TEntity extends IHasId>(): Tracker<TEntity> {
    return new Tracker<TEntity>((a, b) => a.id === b.id);
  }


  setData(data: T[], selected: boolean) {
    this.data.set(data.map(d => ({value: d, selected: selected})));
  }

  /**
   * Will toggle if the data item is selected or not. If data option is not tracked, will add it and set state to true.
   * @param data Item to toggle
   * @param selected Force the state
   */
  toggle(data: T, selected?: boolean) {
    const dataItem = this.data().find(d => this.compareFn(d.value, data));
    if (!dataItem) {
      this.data.update(x => [...x, {value: data, selected: (selected !== undefined ? selected : false)}]);
      return;
    }

    this.data.update(x => [...x.map(item => {
      if (this.compareFn(item.value, data)) {
        return {value: data, selected: (selected !== undefined ? selected : !item.selected)}
      }

      return item;
    })]);
  }

  setAll(selected: boolean) {
    this.data.update(x => [...x.map(item => ({value: item.value, selected: selected}))]);
  }

  /**
   * Is the passed item selected
   * @param data item to check against
   * @returns boolean
   */
  isSelected(data: T) {
    const entry = this.data().find(d => this.compareFn(d.value, data));
    return entry !== undefined && entry.selected;
  }

  public hasSomeSelected: Signal<boolean> = computed(() => {
    const selectedCount = this.data().filter(d => d.selected).length;
    return selectedCount !== this.data().length && selectedCount > 0;
  });

  public allSelected: Signal<boolean> = computed(() => {
    return this.selected().length == this.data().length;
  });

  public selected: Signal<T[]> = computed(() => {
    return [...this.data().filter(x => x.selected).map(d => d.value)];
  });

  public unselected: Signal<T[]>  = computed(() => {
    return [...this.data().filter(x => !x.selected).map(d => d.value)];
  });

}
