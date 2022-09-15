import { SeriesFilter } from "../_models/series-filter";

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
    /**
     * Should the filter section be open by default
     * @deprecated This is deprecated UX pattern. New style is to show highlight on filter button. 
     */
    openByDefault = false;
  }