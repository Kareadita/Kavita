import {LibraryType} from "./library/library";
import {MangaFormat} from "./manga-format";
import {IHasCover} from "./common/i-has-cover";
import {AgeRating} from "./metadata/age-rating";
import {IHasReadingTime} from "./common/i-has-reading-time";
import {IHasCast} from "./common/i-has-cast";

export interface ReadingListItem {
  pagesRead: number;
  pagesTotal: number;
  seriesName: string;
  seriesFormat: MangaFormat;
  seriesId: number;
  chapterId: number;
  order: number;
  chapterNumber: string;
  volumeNumber: string;
  libraryId: number;
  id: number;
  releaseDate: string;
  title: string;
  libraryType: LibraryType;
  libraryName: string;
  summary?: string;
}

export interface ReadingList extends IHasCover {
  id: number;
  title: string;
  summary: string;
  promoted: boolean;
  coverImageLocked: boolean;
  items: Array<ReadingListItem>;
  /**
   * If this is empty or null, the cover image isn't set. Do not use this externally.
  */
  coverImage?: string;
  primaryColor: string;
  secondaryColor: string;
  startingYear: number;
  startingMonth: number;
  endingYear: number;
  endingMonth: number;
  itemCount: number;
  ageRating: AgeRating;
}

export interface ReadingListInfo extends IHasReadingTime, IHasReadingTime {
  pages: number;
  wordCount: number;
  isAllEpub: boolean;
  minHoursToRead: number;
  maxHoursToRead: number;
  avgHoursToRead: number;
}

export interface ReadingListCast extends IHasCast {}
