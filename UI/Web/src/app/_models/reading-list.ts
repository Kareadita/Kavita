import { Chapter } from "./chapter";

export interface ReadingListItem {
    libraryId: number;
    volumeId: number;
    
    seriesId: number;
    chapterId: number;
    order: number;
}

export interface ReadingList {
    id: number;
    title: string;
    summary: string;
    items: Array<ReadingListItem>;
}