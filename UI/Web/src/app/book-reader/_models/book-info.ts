import {MangaFormat} from "src/app/_models/manga-format";

export interface BookInfo {
  bookTitle: string;
  seriesFormat: MangaFormat;
  seriesId: number;
  libraryId: number;
  volumeId: number;
}
