import { Chapter } from "./chapter";

export interface ReadingListItem {
    libraryId: number;
    seriesId: number;
    volumeId: number;
    chapterId: number;
    //chapter: Chapter; // maybe?
    order: number;
}

export interface ReadingList {
    id: number;
    title: string;
    summary: string;
    items: Array<ReadingListItem>;
}