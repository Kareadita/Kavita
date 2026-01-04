import {MangaFormat} from "../manga-format";
import {FilterV2} from "./v2/filter-v2";

export interface FilterItem<T> {
    title: string;
    value: T;
    selected: boolean;
}


export enum SortField {
  SortName = 1,
  Created = 2,
  LastModified = 3,
  LastChapterAdded = 4,
  TimeToRead = 5,
  ReleaseYear = 6,
  /**
   * This sorts on the DATE of last progress
   */
  ReadProgress = 7,
  /**
   * Kavita+ only
   */
  AverageRating = 8,
  Random = 9,
  UserRating = 10,
}

export const allSeriesSortFields = Object.keys(SortField)
    .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
    .map(key => parseInt(key, 10)) as SortField[];

export const mangaFormatFilters = [
    {
      title: 'images',
      value: MangaFormat.IMAGE,
      selected: false
    },
    {
      title: 'epub',
      value: MangaFormat.EPUB,
      selected: false
    },
    {
      title: 'pdf',
      value: MangaFormat.PDF,
      selected: false
    },
    {
      title: 'archive',
      value: MangaFormat.ARCHIVE,
      selected: false
    }
];

export interface FilterEvent<TFilter extends number = number, TSort extends number = number> {
  filterV2: FilterV2<TFilter, TSort>;
  isFirst: boolean;
}

