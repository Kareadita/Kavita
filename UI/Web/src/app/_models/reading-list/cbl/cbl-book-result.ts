import { CblImportReason } from "./cbl-import-reason.enum";

export interface CblBookResult {
    order: number;
    series: string;
    volume: string;
    number: string;
    /**
     * For SeriesCollision
     */
    libraryId: number;
    /**
     * For SeriesCollision
     */
    seriesId: number;
    readingListName: string;
    reason: CblImportReason;
}