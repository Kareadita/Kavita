import { MangaFormat } from "./manga-format";

export interface FilterItem<T> {
    title: string;
    value: T;
    selected: boolean;
}

export interface SeriesFilter {
    formats: Array<MangaFormat>;
    libraries: Array<number>,
    readStatus: ReadStatus;
    genres: Array<number>;
    writers: Array<number>;
    penciller: Array<number>;
    inker: Array<number>;
    colorist: Array<number>;
    letterer: Array<number>;
    coverArtist: Array<number>;
    editor: Array<number>;
    publisher: Array<number>;
    character: Array<number>;
    translators: Array<number>;
    collectionTags: Array<number>;
    rating: number;
    ageRating: Array<number>;
    sortOptions: SortOptions | null;
    tags: Array<number>;
    languages: Array<string>;
    publicationStatus: Array<number>;
}

export interface SortOptions {
  sortField: SortField;
  isAscending: boolean;
}

export enum SortField {
  SortName = 1,
  Created = 2,
  LastModified = 3
}

export interface ReadStatus {
  notRead: boolean,
  inProgress: boolean,
  read: boolean,
}

export const mangaFormatFilters = [
    {
      title: 'Images',
      value: MangaFormat.IMAGE,
      selected: false
    },
    {
      title: 'EPUB',
      value: MangaFormat.EPUB,
      selected: false
    },
    {
      title: 'PDF',
      value: MangaFormat.PDF,
      selected: false
    },
    {
      title: 'ARCHIVE',
      value: MangaFormat.ARCHIVE,
      selected: false
    }
];

export interface FilterEvent {
  filter: SeriesFilter;
  isFirst: boolean;
}

