import {allEnums} from "../../../_helpers/enum";

export enum ReadingListSortField {
  Title = 1,
  ReleaseYearStart = 2,
  ReleaseYearEnd = 3,
  ItemCount = 4
}

export const allReadingListSortFields = allEnums(ReadingListSortField);
