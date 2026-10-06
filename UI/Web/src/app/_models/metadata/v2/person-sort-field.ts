import {allEnums} from "../../../_helpers/enum";

export enum PersonSortField {
  Name = 1,
  SeriesCount = 2,
  ChapterCount = 3
}

export const allPersonSortFields = allEnums(PersonSortField);
