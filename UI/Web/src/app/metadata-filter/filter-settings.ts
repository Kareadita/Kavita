import {FilterV2} from "../_models/metadata/v2/filter-v2";

export class FilterSettings<T> {
    presetsV2: FilterV2<T> | undefined;
    sortDisabled = false;
    /**
     * The number of statements that can be on the filter. Set to 1 to disable adding more.
     */
    statementLimit: number = 0;
    saveDisabled: boolean = false;
  }
