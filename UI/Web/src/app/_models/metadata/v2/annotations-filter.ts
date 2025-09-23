

export enum AnnotationsFilterField {
  Owner = 1,
  Series = 2,
  Library = 3,
  Colour = 4,
  Spoiler = 5
}

export const allAnnotationsFilterFields = Object.keys(AnnotationsFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsFilterField[];

export enum AnnotationsSortField {
  Owner = 1,
  Series = 2,
}

export const allAnnotationsSortFields = Object.keys(AnnotationsSortField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsSortField[];
