import {Person} from "../person";

export interface BrowsePerson extends Person {
  seriesCount: number;
  chapterCount: number;
}
