import { Chapter } from './chapter';
import { HourEstimateRange } from './series-detail/hour-estimate-range';
import {IHasCover} from "./common/i-has-cover";
import {IHasReadingTime} from "./common/i-has-reading-time";
import {IHasProgress} from "./common/i-has-progress";

export interface Volume extends IHasCover, IHasReadingTime, IHasProgress {
    id: number;
    minNumber: number;
    maxNumber: number;
    name: string;
    createdUtc: string;
    lastModifiedUtc: string;
    pages: number;
    pagesRead: number;
    wordCount: number;
    chapters: Array<Chapter>;
    /**
     * This is only available on the object when fetched for SeriesDetail
     */
    timeEstimate?: HourEstimateRange;
    minHoursToRead: number;
    maxHoursToRead: number;
    avgHoursToRead: number;

    coverImage?: string;
    coverImageLocked: boolean;
    primaryColor: string;
    secondaryColor: string;
}
