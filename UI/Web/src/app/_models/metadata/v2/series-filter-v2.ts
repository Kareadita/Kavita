import { SortOptions } from "../series-filter";
import { FilterGroup } from "./filter-group";

export interface SeriesFilterV2 {
    name: string;
    groups: Array<FilterGroup>;
    sortOptions: SortOptions;
}