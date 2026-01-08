import {MangaFormat} from "../manga-format";

export interface ReadingHistoryItem {
  sessionId: number;
  startTimeUtc: string;
  endTimeUtc: string;
  localDate: string;

  seriesId: number;
  seriesName: string;
  seriesFormat: MangaFormat;

  chapterId: number;
  chapterTitle: string;
  chapterNumber: string;

  libraryId: number;
  libraryName: string;

  pagesRead: number;
  wordsRead: number;
  durationSeconds: number;

  startPage: number;
  endPage: number;
  totalPages: number;
  completed: boolean;
}
