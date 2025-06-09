export enum PersonFilterField {
  None = -1,
  Role = 1,
  Name = 2
}


export const allPersonFilterFields = Object.keys(PersonFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as PersonFilterField[];

