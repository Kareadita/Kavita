import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {SortField} from "../_models/metadata/series-filter";
import {PersonSortField} from "../_models/metadata/v2/person-sort-field";
import {PersonFilterField} from "../_models/metadata/v2/person-filter-field";
import {FilterField} from "../_models/metadata/v2/filter-field";

/**
 * The set of entities that are supported for rich filtering. Each entity must have its own distinct SortField and FilterField enums.
 */
export type ValidFilterEntity = 'series' | 'person';

export class FilterSettingsBase<TFilter extends number = number, TSort extends number = number> {
    presetsV2: FilterV2<TFilter, TSort> | undefined;
    sortDisabled = false;
    /**
     * The number of statements that can be on the filter. Set to 1 to disable adding more.
     */
    statementLimit: number = 0;
    saveDisabled: boolean = false;
    type: ValidFilterEntity = 'series';
    supportsSmartFilter: boolean = false;
}

/**
 * Filter Settings for Series entity
 */
export class SeriesFilterSettings extends FilterSettingsBase<FilterField, SortField> {
  type: ValidFilterEntity = 'series';
  supportsSmartFilter = true;
}

/**
 * Filter Settings for People entity
 */
export class PersonFilterSettings extends FilterSettingsBase<PersonFilterField, PersonSortField> {
  type: ValidFilterEntity = 'person';
}


