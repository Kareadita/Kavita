import { Chapter } from "../chapter";
import { Volume } from "../volume";
import {LibraryType} from "../library/library";

/**
 * This is built for Series Detail itself
 */
export interface SeriesDetail {
    specials: Array<Chapter>;
    chapters: Array<Chapter>;
    volumes: Array<Volume>;
    libraryType: LibraryType;
    storylineChapters: Array<Chapter>;
    unreadCount: number;
    totalCount: number;
}
