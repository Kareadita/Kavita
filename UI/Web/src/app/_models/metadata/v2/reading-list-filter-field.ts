export enum ReadingListFilterField {
  Title = 1,
  ReleaseYear = 2,
  ItemCount = 3,
  Tags = 4,
  Writer = 5,
  Artist = 6,
  Provider = 7,
  MissingItemCount = 8
}

export const allReadingListFilterFields = Object.keys(ReadingListFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as ReadingListFilterField[];

