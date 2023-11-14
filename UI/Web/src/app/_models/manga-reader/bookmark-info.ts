import { FileDimension } from "src/app/manga-reader/_models/file-dimension";
import { LibraryType } from "../library/library";
import { MangaFormat } from "../manga-format";

export interface BookmarkInfo {
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    libraryId: number;
    libraryType: LibraryType;
    pages: number;
    /**
     * This will not always be present. Depends on if asked from backend.
     */
    pageDimensions?: Array<FileDimension>;
    /**
     * This will not always be present. Depends on if asked from backend.
     */
    doublePairs?: {[key: number]: number};
}
