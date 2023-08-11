import { SortOptions } from "../series-filter";
import {FilterStatement} from "./filter-statement";
import {FilterCombination} from "./filter-combination";

export interface SeriesFilterV2 {
    name?: string;
    statements: Array<FilterStatement>;
    combination: FilterCombination;
    sortOptions?: SortOptions;
    limitTo: number;
}
