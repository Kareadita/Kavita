import {LibraryType} from "./library/library";
import {MangaFormat} from "./manga-format";
import {IHasCover} from "./common/i-has-cover";
import {AgeRating} from "./metadata/age-rating";
import {IHasReadingTime} from "./common/i-has-reading-time";
import {IHasCast} from "./common/i-has-cast";

export interface ReadingListItemChapter {
  id: number;
  range: string;
  titleName?: string;
  minNumber: number;
  maxNumber: number;
  sortOrder: number;
  pages: number;
  isSpecial: boolean;
  releaseDate: string;
  summary?: string;
  writerName?: string;
  writerId?: number;
  pencillerName?: string;
  pencillerId?: number;
}

export interface ReadingListItemVolume {
  id: number;
  name: string;
  minNumber: number;
  maxNumber: number;
  seriesId: number;
}

export interface ReadingListItem {
  pagesRead: number;
  pagesTotal: number;
  seriesName: string;
  seriesSortName: string;
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
  chapter: ReadingListItemChapter;
  volume: ReadingListItemVolume;
}

export enum ReadingListProvider {
  /** Default, List created within Kavita. No Sync capabilities */
  None = 0,
  /** Created by File upload. No Sync capabilities */
  File = 1,
  /** Downloaded via CBL Manager or direct Url feed */
  Url = 2
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

  sourcePath: string | null;
  downloadUrl: string | null;
  shareUrl: string | null;
  provider: ReadingListProvider;
  lastSyncCheckUtc: Date | null;
  lastSyncedDate: Date | null;
  canSync: boolean;
  hasRemoteChange: boolean;
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
