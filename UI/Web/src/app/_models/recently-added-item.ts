import { LibraryType } from "./library";

export interface RecentlyAddedItem {
    seriesId: number;
    seriesName: string;
    created: string;
    title: string;
    libraryId: number;
    libraryType: LibraryType;
    volumeId: number;
    chapterId: number; 
    id: number; // This is UI only
}