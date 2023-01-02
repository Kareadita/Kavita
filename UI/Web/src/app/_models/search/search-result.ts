import { MangaFormat } from "../manga-format";

export interface SearchResult {
    seriesId: number;
    libraryId: number;
    libraryName: string;
    name: string;
    originalName: string;
    localizedName: string;
    sortName: string;
    format: MangaFormat;
}
