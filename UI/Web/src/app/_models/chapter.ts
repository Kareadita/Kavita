import { HourEstimateRange } from './series-detail/hour-estimate-range';
import { MangaFile } from './manga-file';
import { AgeRating } from './metadata/age-rating';
import { AgeRatingDto } from './metadata/age-rating-dto';

/**
 * Chapter table object. This does not have metadata on it, use ChapterMetadata which is the same Chapter but with those fields.
 */
export interface Chapter {
    id: number;
    range: string;
    number: string;
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
    created: string;
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
}
