import {Genre} from "../genre";

export interface BrowseGenre extends Genre {
  seriesCount: number;
  chapterCount: number;
}
