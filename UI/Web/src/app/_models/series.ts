import { MangaFormat } from './manga-format';
import { Volume } from './volume';
import {IHasCover} from "./common/i-has-cover";
import {IHasReadingTime} from "./common/i-has-reading-time";
import {IHasProgress} from "./common/i-has-progress";

export interface Series extends IHasCover, IHasReadingTime, IHasProgress {
  id: number;
  name: string;
  /**
   * This is not shown to user
   */
  originalName: string;
  localizedName: string;
  sortName: string;
  coverImageLocked: boolean;
  sortNameLocked: boolean;
  localizedNameLocked: boolean;
  nameLocked: boolean;
  volumes: Volume[];
  /**
   * Total pages in series
   */
  pages: number;
  /**
   * Total pages the logged in user has read
   */
  pagesRead: number;
  /**
   * User's rating (0-5)
   */
  userRating: number;
  hasUserRated: boolean;
  libraryId: number;
  /**
   * DateTime the entity was created
   */
  created: string;
  /**
   * Format of the Series
   */
  format: MangaFormat;
  /**
   * DateTime that represents last time the logged in user read this series
   */
  latestReadDate: string;
  /**
   * DateTime representing last time a chapter was added to the Series
   */
  lastChapterAdded: string;
  /**
   * DateTime representing last time the series folder was scanned
   */
  lastFolderScanned: string;
  /**
   * Number of words in the series
   */
  wordCount: number;
  minHoursToRead: number;
  maxHoursToRead: number;
  avgHoursToRead: number;
  /**
   * Highest level folder containing this series
   */
  folderPath: string;
  lowestFolderPath: string;
  /**
   * This is currently only used on Series detail page for recommendations
   */
  summary?: string;
  coverImage?: string;
  primaryColor: string;
  secondaryColor: string;
  /**
   * Kavita+ only. Will not perform any matching from Kavita+
   */
  dontMatch: boolean;
  /**
   * Kavita+ only. Did this series not match and won't without manual match
   */
  isBlacklisted: boolean;
}
