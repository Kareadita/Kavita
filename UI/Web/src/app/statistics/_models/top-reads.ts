import {StatCount} from "./stat-count";

export interface TopUserRead {
    userId: number;
    username: string;
    mangaTime: number;
    comicsTime: number;
    booksTime: number;
}

export interface MostActiveUser {
  userId: number;
  username: string;
  coverImage?: string;
  totalHoursRead: number;
  librariesRead: Array<StatCount<number>>;
}
