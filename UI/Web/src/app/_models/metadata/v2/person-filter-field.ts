export enum PersonFilterField {
  Role = 1,
  Name = 2,
  SeriesCount = 3,
  ChapterCount = 4,
}


export const allPersonFilterFields = Object.keys(PersonFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as PersonFilterField[];

