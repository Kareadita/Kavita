import {MangaFormat} from "../manga-format";


export type RereadPrompt = {
  shouldPrompt: boolean;
  timePrompt: boolean;
  daysSinceLastRead: number;
  chapterOnContinue: RereadChapter;
  chapterOnReread: RereadChapter;
}

export type RereadChapter = {
  libraryId: number;
  seriesId: number;
  chapterId: number;
  label: string;
  format: MangaFormat,
}
