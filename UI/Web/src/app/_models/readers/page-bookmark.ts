import {Series} from "../series";

export interface PageBookmark {
  id: number;
  page: number;
  seriesId: number;
  volumeId: number;
  chapterId: number;
  /**
   * Only present on epub-based Bookmarks
   */
  imageOffset: number;
  /**
   * Only present on epub-based Bookmarks
   */
  xPath: string | null;
  /**
   * This is only used when getting all bookmarks.
   */
  series: Series | null;
  /**
   * Chapter name (from ToC) or Title (from ComicInfo/PDF)
   */
  chapterTitle: string | null;
}
