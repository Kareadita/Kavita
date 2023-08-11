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
    presetsV2: SeriesFilterV2 | undefined;
  }
