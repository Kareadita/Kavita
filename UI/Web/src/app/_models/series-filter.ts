import { MangaFormat } from "./manga-format";

export interface FilterItem {
    title: string;
    value: any;
    selected: boolean;
}

export interface SeriesFilter {
    mangaFormat: MangaFormat | null;
}