import {PersonSortField} from "./person-sort-field";

/**
 * Series-based Sort options
 */
export interface SortOptions<TSort extends number = number> {
  sortField: TSort;
  isAscending: boolean;
}

/**
 * Person-based Sort Options
 */
export interface PersonSortOptions {
  sortField: PersonSortField;
  isAscending: boolean;
}
