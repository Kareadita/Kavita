import {PrefetchChapterInfo} from "./prefetch-chapterinfo";

export type ChapterCapInfo = {
  prevChapter: PrefetchChapterInfo;
  nextChapter: PrefetchChapterInfo;
  nextChapterDisabled: boolean;
  readingListMode: boolean;
};
