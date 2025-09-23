

export enum AnnotationsFilterField {
  Owner = 1,
  Library = 2,
  Spoiler = 3,
}

export const allAnnotationsFilterFields = Object.keys(AnnotationsFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsFilterField[];

export enum AnnotationsSortField {
  Owner = 1,
  Created = 2,
  LastModified = 3,
}

export const allAnnotationsSortFields = Object.keys(AnnotationsSortField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsSortField[];
