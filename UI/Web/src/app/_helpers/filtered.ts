import {computed, Signal} from "@angular/core";

/** A field to match against: a key of the item, or a function deriving a value from it */
export type FilterField<T> = keyof T | ((item: T) => unknown);

/**
 * Whether one item matches the query on any of the given fields. Use for a `FilterPipe` callback,
 * where the data is not a signal; prefer {@link filteredBy} when it is.
 */
export function matchesQuery<T>(item: T, query: string, ...fields: FilterField<T>[]): boolean {
  return matches(item, fields, query.trim().toLowerCase());
}

function matches<T>(item: T, fields: FilterField<T>[], query: string): boolean {
  if (!query) return true;
  if (fields.length === 0) return String(item ?? '').toLowerCase().includes(query);

  return fields.some(field => {
    const value = typeof field === 'function' ? field(item) : item[field];
    return String(value ?? '').toLowerCase().includes(query);
  });
}

/**
 * Case-insensitive substring match of `query` against the given fields of each item.
 *
 * Values are coerced, so numbers and null/undefined fields are safe to pass. Pass no fields to match
 * against the items themselves (a `string[]`). An empty query returns `data` unchanged rather than a
 * copy, so downstream `track` and OnPush checks see the same reference.
 */
function filtered<T>(data: T[] | null | undefined, query: string, ...fields: FilterField<T>[]): T[] {
  if (!data || data.length === 0) return [];

  const queryString = query.trim().toLowerCase();
  if (!queryString) return data;

  return data.filter(item => matches(item, fields, queryString));
}

/**
 * Signal form of {@link filtered}, for a `filteredData` field:
 * `filteredData = filteredBy(this.data, this.filterQuery, 'comment', 'details');`
 */
export function filteredBy<T>(data: Signal<T[] | null | undefined>, query: Signal<string>,
                              ...fields: FilterField<T>[]): Signal<T[]> {
  return computed(() => filtered(data(), query(), ...fields));
}
