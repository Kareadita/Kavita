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
  series: Series;
}
