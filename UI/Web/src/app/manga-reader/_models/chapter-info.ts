import { MangaFormat } from "src/app/_models/manga-format";

export interface ChapterInfo {
    chapterNumber: string;
    volumeNumber: string;
    chapterTitle: string;
    seriesName: string;
    seriesFormat: MangaFormat;
    seriesId: number;
    libraryId: number;
    fileName: string;
    isSpecial: boolean;
    volumeId: number;
    pages: number;
}