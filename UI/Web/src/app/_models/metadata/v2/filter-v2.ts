import {FilterStatement} from "./filter-statement";
import {FilterCombination} from "./filter-combination";
import {SortOptions} from "./sort-options";

export interface FilterV2<TFilter extends number = number, TSort extends number = number> {
    name?: string;
    statements: Array<FilterStatement<TFilter>>;
    combination: FilterCombination;
    sortOptions?: SortOptions<TSort>;
    limitTo: number;
}
