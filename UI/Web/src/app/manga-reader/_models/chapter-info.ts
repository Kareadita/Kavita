import {FileDimension} from "./file-dimension";
import {LibraryType} from "../../_models/library/library";
import {MangaFormat} from "../../_models/manga-format";

export interface ChapterInfo {
    chapterNumber: string;
    volumeNumber: string;
    chapterTitle: string;
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    libraryId: number;
    libraryType: LibraryType;
    fileName: string;
    isSpecial: boolean;
    volumeId: number;
    pages: number;
    subtitle: string;
    title: string;
    /**
     * This will not always be present. Depends on if asked from backend.
     */
    pageDimensions?: Array<FileDimension>;
    /**
     * This will not always be present. Depends on if asked from backend.
     */
    doublePairs?: {[key: number]: number};
    seriesTotalPagesRead: number;
    seriesTotalPages: number;
}
