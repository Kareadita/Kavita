import { LibraryType } from "src/app/_models/library";
import { MangaFormat } from "src/app/_models/manga-format";

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
}