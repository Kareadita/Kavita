import { SeriesFilterV2 } from "../_models/metadata/v2/series-filter-v2";

export class FilterSettings {
    sortDisabled = false;
    presetsV2: SeriesFilterV2 | undefined;
    /**
     * The number of statements that can be on the filter. Set to 1 to disable adding more.
     */
    statementLimit: number = 0;
    saveDisabled: boolean = false;
  }
