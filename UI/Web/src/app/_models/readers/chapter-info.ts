import {FileDimension} from "../../manga-reader/_models/file-dimension";
import {LibraryType} from "../library/library";
import {MangaFormat} from "../manga-format";
import {PublicationStatus} from "../metadata/publication-status";

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
    seriesPublicationStatus: PublicationStatus | null;
}
