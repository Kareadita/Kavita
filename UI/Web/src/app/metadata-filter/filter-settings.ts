import { SeriesFilter } from "../_models/metadata/series-filter";
import { SeriesFilterV2 } from "../_models/metadata/v2/series-filter-v2";

export class FilterSettings {
    libraryDisabled = false;
    formatDisabled = false;
    collectionDisabled = false;
    genresDisabled = false;
    peopleDisabled = false;
    readProgressDisabled = false;
    ratingDisabled = false;
    sortDisabled = false;
    ageRatingDisabled = false;
    tagsDisabled = false;
    languageDisabled = false;
    publicationStatusDisabled = false;
    searchNameDisabled = false;
    releaseYearDisabled = false;
    presets: SeriesFilter | undefined;
    presetsV2: SeriesFilterV2 | undefined;
    /**
     * Should the filter section be open by default
     * @deprecated This is deprecated UX pattern. New style is to show highlight on filter button.
     */
    openByDefault = false;
  }
