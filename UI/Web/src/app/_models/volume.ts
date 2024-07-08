import { Chapter } from './chapter';
import { HourEstimateRange } from './series-detail/hour-estimate-range';

export interface Volume {
    id: number;
    minNumber: number;
    maxNumber: number;
    name: string;
    createdUtc: string;
    lastModifiedUtc: string;
    pages: number;
    pagesRead: number;
    chapters: Array<Chapter>;
    /**
     * This is only available on the object when fetched for SeriesDetail
     */
    timeEstimate?: HourEstimateRange;
    minHoursToRead: number;
    maxHoursToRead: number;
    avgHoursToRead: number;
}
