import {FilterStatement} from "./filter-statement";
import {FilterCombination} from "./filter-combination";
import {SortOptions} from "./sort-options";

export interface SeriesFilterV2 {
    name?: string;
    statements: Array<FilterStatement>;
    combination: FilterCombination;
    sortOptions?: SortOptions;
    limitTo: number;
}
