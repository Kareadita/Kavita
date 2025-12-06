import {Chapter} from "../../_models/chapter";

export type FavoriteAuthor = {
  authorId: number;
  authorName: string;
  totalChaptersRead: number;
  chapters: Chapter[];
}
