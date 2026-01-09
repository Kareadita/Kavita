import {MangaFormat} from "../manga-format";

export interface ReadingHistoryItem {
  sessionDataIds: number[];
  id: number;
  sessionId: number;
  startTimeUtc: string;
  endTimeUtc: string;
  localDate: string;

  seriesId: number;
  seriesName: string;
  seriesFormat: MangaFormat;

  chapters: ReadingHistoryChapterItem[];

  libraryId: number;
  libraryName: string;

  pagesRead: number;
  wordsRead: number;
  durationSeconds: number;

  totalPages: number;
}

export interface ReadingHistoryChapterItem {
  chapterId: number;
  label: string;

  startTimeUtc: string;
  endTimeUtc: string;

  pagesRead: number;
  wordsRead: number;
  durationSeconds: number;

  startPage: number;
  endPage: number;
  totalPages: number;
  completed: boolean;
}
