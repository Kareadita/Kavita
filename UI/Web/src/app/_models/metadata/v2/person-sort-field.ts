export enum PersonSortField {
  Name = 1,
  SeriesCount = 2,
  ChapterCount = 3
}

export const allPersonSortFields = Object.keys(PersonSortField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as PersonSortField[];
