import {FilterStatement} from "./filter-statement";
import {FilterCombination} from "./filter-combination";
import {SortOptions} from "./sort-options";

export interface FilterV2<T> {
    name?: string;
    statements: Array<FilterStatement<T>>;
    combination: FilterCombination;
    sortOptions?: SortOptions;
    limitTo: number;
}
