import {ChapterInfo} from "./chapter-info";

export type PrefetchChapterInfo = {chapterId: number, info: ChapterInfo} | null;

export type ContinuousReaderInfo = {
  showStartCap: boolean;
  showEndCap: boolean;
  prevChapter: PrefetchChapterInfo;
  nextChapter: PrefetchChapterInfo;
  nextHint: string;
  prevHint: string;
  nextHeading: string;
};
