import { MangaFormat } from "../manga-format";
import { SeriesFilterV2 } from "./v2/series-filter-v2";
import {FilterField} from "./v2/filter-field";

export interface FilterItem<T> {
    title: string;
    value: T;
    selected: boolean;
}

export interface SortOptions {
  sortField: SortField;
  isAscending: boolean;
}

export enum SortField {
  SortName = 1,
  Created = 2,
  LastModified = 3,
  LastChapterAdded = 4,
  TimeToRead = 5,
  ReleaseYear = 6,
  ReadProgress = 7,
  /**
   * Kavita+ only
   */
  AverageRating = 8,
  Random = 9
}

export const allSortFields = Object.keys(SortField)
    .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
    .map(key => parseInt(key, 10)) as SortField[];

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
  filterV2: SeriesFilterV2;
  isFirst: boolean;
}

