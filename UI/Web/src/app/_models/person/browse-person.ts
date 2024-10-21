import {Person} from "../metadata/person";

export interface BrowsePerson extends Person {
  seriesCount: number;
  issueCount: number;
}
