import {SortField} from "../series-filter";
import {PersonSortField} from "./person-sort-field";

/**
 * Series-based Sort options
 */
export interface SortOptions {
  sortField: SortField;
  isAscending: boolean;
}

/**
 * Person-based Sort Options
 */
export interface PersonSortOptions {
  sortField: PersonSortField;
  isAscending: boolean;
}
