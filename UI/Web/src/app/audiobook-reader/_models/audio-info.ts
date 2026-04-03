import {MangaFormat} from "../../_models/manga-format";

export interface AudioInfo {
  bookTitle: string;
  seriesId: number;
  volumeId: number;
  seriesFormat: MangaFormat;
  seriesName: string;
  chapterNumber: string;
  volumeNumber: string;
  libraryId: number;
  /** Total duration in seconds */
  pages: number;
  isSpecial: boolean;
  chapterTitle: string;
  author: string;
  narrator: string;
}
