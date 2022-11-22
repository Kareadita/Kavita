import { CollectionTag } from "../collection-tag";
import { Genre } from "./genre";
import { AgeRating } from "./age-rating";
import { PublicationStatus } from "./publication-status";
import { Person } from "./person";
import { Tag } from "../tag";

export interface SeriesMetadata {
    seriesId: number;
    summary: string;

    totalCount: number;
    maxCount: number;

    collectionTags: Array<CollectionTag>;
    genres: Array<Genre>;
    tags: Array<Tag>;
    writers: Array<Person>;
    coverArtists: Array<Person>;
    publishers: Array<Person>;
    characters: Array<Person>;
    pencillers: Array<Person>;
    inkers: Array<Person>;
    colorists: Array<Person>;
    letterers: Array<Person>;
    editors: Array<Person>;
    translators: Array<Person>;
    ageRating: AgeRating;
    releaseYear: number;
    language: string;
    publicationStatus: PublicationStatus;

    summaryLocked: boolean;
    genresLocked: boolean;
    tagsLocked: boolean;
    writersLocked: boolean;
    coverArtistsLocked: boolean;
    publishersLocked: boolean;
    charactersLocked: boolean;
    pencillersLocked: boolean;
    inkersLocked: boolean;
    coloristsLocked: boolean;
    letterersLocked: boolean;
    editorsLocked: boolean;
    translatorsLocked: boolean;
    ageRatingLocked: boolean;
    releaseYearLocked: boolean;
    languageLocked: boolean;
    publicationStatusLocked: boolean;
}