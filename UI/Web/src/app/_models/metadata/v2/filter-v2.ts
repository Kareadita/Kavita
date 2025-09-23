import {FilterStatement} from "./filter-statement";
import {FilterCombination} from "./filter-combination";
import {SortOptions} from "./sort-options";
import {AnnotationsFilterField, AnnotationsSortField} from "./annotations-filter";

export interface FilterV2<TFilter extends number = number, TSort extends number = number> {
    name?: string;
    statements: Array<FilterStatement<TFilter>>;
    combination: FilterCombination;
    sortOptions?: SortOptions<TSort>;
    limitTo: number;
}

export type AnnotationsFilter = FilterV2<AnnotationsFilterField, AnnotationsSortField>;
