import {LibraryType} from "./library/library";
import {MangaFormat} from "./manga-format";

export interface SeriesGroup {
    seriesId: number;
    seriesName: string;
    created: string;
    title: string;
    libraryId: number;
    libraryType: LibraryType;
    volumeId: number;
    chapterId: number;
    format: MangaFormat;
    id: number;  // This is UI only, sent from backend but has no relation to any entity
    count: number;
}
