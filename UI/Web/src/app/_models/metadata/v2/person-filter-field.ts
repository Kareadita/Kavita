import {allEnums} from "../../../_helpers/enum";

export enum PersonFilterField {
  Role = 1,
  Name = 2,
  SeriesCount = 3,
  ChapterCount = 4,
  Library = 5,
}


export const allPersonFilterFields = allEnums(PersonFilterField);

