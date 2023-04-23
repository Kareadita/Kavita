import { FilterStatement } from "src/app/_models/metadata/v2/filter-statement";

export interface FilterGroup {
    and: Array<FilterGroup>;
    or: Array<FilterGroup>;
    statements: Array<FilterStatement>;
}