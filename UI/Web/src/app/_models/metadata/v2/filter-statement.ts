import {FilterComparison} from "./filter-comparison";

export interface FilterStatement<T extends number = number> {
    comparison: FilterComparison;
    field: T;
    value: string;
}
