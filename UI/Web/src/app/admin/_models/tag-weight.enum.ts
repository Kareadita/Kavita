export enum TagWeight {
  Core = 1,
  Defining = 2,
  Recurrent = 3,
  Incidental = 4,
  Unweighted = 5,
}

export const allTagWeights = Object.keys(TagWeight)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as TagWeight[];
