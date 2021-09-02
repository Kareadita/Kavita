import { MangaFormat } from "./manga-format";

export interface ReadingListItem {
    pagesRead: number;
    totalPages: number;
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    chapterId: number;
    order: number;
    chapterNumber: string;
    volumeNumber: string;
}

export interface ReadingList {
    id: number;
    title: string;
    summary: string;
    items: Array<ReadingListItem>;
}