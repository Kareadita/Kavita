import { LibraryType } from "./library";
import { MangaFormat } from "./manga-format";

export interface ReadingListItem {
    pagesRead: number;
    pagesTotal: number;
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    chapterId: number;
    order: number;
    chapterNumber: string;
    volumeNumber: string;
    libraryId: number;
    id: number;
    releaseDate: string;
    title: string;
    libraryType: LibraryType;
    libraryName: string;
}

export interface ReadingList {
    id: number;
    title: string;
    summary: string;
    promoted: boolean;
    coverImageLocked: boolean;
    items: Array<ReadingListItem>;
    /**
     * If this is empty or null, the cover image isn't set. Do not use this externally. 
     */
     coverImage: string;
}