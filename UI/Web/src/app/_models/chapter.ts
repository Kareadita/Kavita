import { MangaFile } from './manga-file';
import { AgeRating } from './metadata/age-rating';
import {PublicationStatus} from "./metadata/publication-status";
import {Genre} from "./metadata/genre";
import {Tag} from "./tag";
import {Person} from "./metadata/person";

export const LooseLeafOrDefaultNumber = -100000;
export const SpecialVolumeNumber = 100000;

/**
 * Chapter table object. This does not have metadata on it, use ChapterMetadata which is the same Chapter but with those fields.
 */
export interface Chapter {
    id: number;
    range: string;
    /**
     * @deprecated Use minNumber/maxNumber
     */
    number: string;
    minNumber: number;
    maxNumber: number;
    files: Array<MangaFile>;
    /**
     * This is used in the UI, it is not updated or sent to Backend
     */
    coverImage: string;
    coverImageLocked: boolean;
    pages: number;
    volumeId: number;
    pagesRead: number; // Attached for the given user when requesting from API
    isSpecial: boolean;
    title: string;
    createdUtc: string;
    /**
     * Actual name of the Chapter if populated in underlying metadata
     */
    titleName: string;
    /**
     * Summary for the chapter
     */
    summary?: string;
    minHoursToRead: number;
    maxHoursToRead: number;
    avgHoursToRead: number;

    ageRating: AgeRating;
    releaseDate: string;
    wordCount: number;
    /**
     * 'Volume number'. Only available for SeriesDetail
     */
    volumeTitle?: string;
    webLinks: string;
    isbn: string;
    lastReadingProgress: string;
    sortOrder: number;

  // originally in ChapterMetadata but now inlined with Chapter data

  year: string;
  language: string;
  publicationStatus: PublicationStatus;
  count: number;
  totalCount: number;

  genres: Array<Genre>;
  tags: Array<Tag>;
  writers: Array<Person>;
  coverArtists: Array<Person>;
  publishers: Array<Person>;
  characters: Array<Person>;
  pencillers: Array<Person>;
  inkers: Array<Person>;
  imprints: Array<Person>;
  colorists: Array<Person>;
  letterers: Array<Person>;
  editors: Array<Person>;
  translators: Array<Person>;
  teams: Array<Person>;
  locations: Array<Person>;
}
