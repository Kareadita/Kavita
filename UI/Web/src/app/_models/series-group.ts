import { LibraryType } from "./library/library";

export interface SeriesGroup {
    seriesId: number;
    seriesName: string;
    created: string;
    title: string;
    libraryId: number;
    libraryType: LibraryType;
    volumeId: number;
    chapterId: number;
    id: number;  // This is UI only, sent from backend but has no relation to any entity
    count: number;
}
