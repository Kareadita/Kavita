import { LibraryType } from "../library";
import { MangaFormat } from "../manga-format";

export interface BookmarkInfo {
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    libraryId: number;
    libraryType: LibraryType;
    pages: number;
}