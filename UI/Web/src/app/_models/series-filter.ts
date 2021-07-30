import { MangaFormat } from "./manga-format";

export interface FilterItem {
    title: string;
    value: any;
    selected: boolean;
}

export interface SeriesFilter {
    mangaFormat: MangaFormat | null;
}

export const mangaFormatFilters = [
    {
      title: 'Format: All',
      value: null,
      selected: false
    },
    {
      title: 'Format: Images',
      value: MangaFormat.IMAGE,
      selected: false
    },
    {
      title: 'Format: EPUB',
      value: MangaFormat.EPUB,
      selected: false
    },
    {
      title: 'Format: PDF',
      value: MangaFormat.PDF,
      selected: false
    },
    {
      title: 'Format: ARCHIVE',
      value: MangaFormat.ARCHIVE,
      selected: false
    }
];