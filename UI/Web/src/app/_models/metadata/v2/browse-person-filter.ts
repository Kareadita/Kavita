import {PersonRole} from "../person";
import {PersonSortOptions} from "./sort-options";

export interface BrowsePersonFilter {
  roles: Array<PersonRole>;
  query?: string;
  sortOptions?: PersonSortOptions;
}
