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
}

export interface ReadingList {
    id: number;
    title: string;
    summary: string;
    promoted: boolean;
    coverImageLocked: boolean;
    items: Array<ReadingListItem>;
}