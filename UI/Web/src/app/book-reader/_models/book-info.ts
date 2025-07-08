import {MangaFormat} from "src/app/_models/manga-format";

export interface BookInfo {
  bookTitle: string;
  seriesFormat: MangaFormat;
  seriesId: number;
  libraryId: number;
  volumeId: number;
  /**
   * Maps the page number to character count. Only available on epub reader.
   */
  pageWordCounts: {[key: number]: number};
}
