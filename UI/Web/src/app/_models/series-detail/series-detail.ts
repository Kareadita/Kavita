import { Chapter } from "../chapter";
import { Volume } from "../volume";

/**
 * This is built for Series Detail itself
 */
export interface SeriesDetail {
    specials: Array<Chapter>;
    chapters: Array<Chapter>;
    volumes: Array<Volume>;
    storylineChapters: Array<Chapter>;
    unreadCount: number;
    totalCount: number;
}