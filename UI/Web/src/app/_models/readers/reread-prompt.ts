import {MangaFormat} from "../manga-format";


export type RereadPrompt = {
  shouldPrompt: boolean;
  timePrompt: boolean;
  fullReread: boolean;
  daysSinceLastRead: number;
  chapterOnContinue: RereadChapter;
  chapterOnReread: RereadChapter;
}

export type RereadChapter = {
  libraryId: number;
  seriesId: number;
  volumeId: number;
  chapterId: number;
  label: string;
  format: MangaFormat,
}
