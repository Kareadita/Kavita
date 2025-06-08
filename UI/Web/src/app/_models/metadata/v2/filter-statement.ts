import {FilterComparison} from "./filter-comparison";

export interface FilterStatement<T> {
    comparison: FilterComparison;
    field: T;
    value: string;
}
