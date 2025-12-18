import {Series} from "../../_models/series";

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
  timePeriodHours: number;
  totalHours: number;
  totalComics: number;
  totalBooks: number;
  topSeries: Series[];
}
