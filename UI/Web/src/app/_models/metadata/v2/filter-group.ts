import { FilterStatement } from "src/app/_models/metadata/v2/filter-statement";

export interface FilterGroup {
    id?: string;
    and: Array<FilterGroup>;
    or: Array<FilterGroup>;
    statements: Array<FilterStatement>;
}