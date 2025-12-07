
export type FavoriteAuthor = {
  authorId: number;
  authorName: string;
  totalChaptersRead: number;
  chapters: AuthorChapter[];
}

export type AuthorChapter = {
  libraryId: number;
  seriesId: number;
  chapterId: number;
  title: string;
}
