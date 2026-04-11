export enum ReadingListSortField {
  Title = 1,
  ReleaseYearStart = 2,
  ReleaseYearEnd = 3,
  ItemCount = 4
}

export const allReadingListSortFields = Object.keys(ReadingListSortField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as ReadingListSortField[];
