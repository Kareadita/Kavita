import {MangaFormat} from "../manga-format";


export type ReReadPrompt = {
  shouldPrompt: boolean;
  timePrompt: boolean;
  daysSinceLastRead: number;
  chapterOnContinue: ReReadChapter;
  chapterOnReRead: ReReadChapter;
}

export type ReReadChapter = {
  libraryId: number;
  seriesId: number;
  chapterId: number;
  label: string;
  format: MangaFormat,
}
