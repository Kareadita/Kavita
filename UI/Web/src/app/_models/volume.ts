import { Chapter } from './chapter';
import { HourEstimateRange } from './hour-estimate-range';

export interface Volume {
    id: number;
    number: number;
    name: string;
    created: string;
    lastModified: string;
    pages: number;
    pagesRead: number;
    chapters: Array<Chapter>;
    /**
     * This is only available on the object when fetched for SeriesDetail
     */
     timeEstimate?: HourEstimateRange; 
}
