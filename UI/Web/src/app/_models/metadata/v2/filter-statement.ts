import { FilterComparison } from "./filter-comparison";
import { FilterField } from "./filter-field";

export interface FilterStatement {
    comparison: FilterComparison;
    field: FilterField;
    value: string;
}