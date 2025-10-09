import {FilterV2} from "./filter-v2";


export enum AnnotationsFilterField {
  Owner = 1,
  Library = 2,
  Spoiler = 3,
  HighlightSlots = 4,
  Selection = 5,
  Comment = 6,
  Series = 7
}

export const allAnnotationsFilterFields = Object.keys(AnnotationsFilterField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsFilterField[];

export enum AnnotationsSortField {
  Owner = 1,
  Created = 2,
  LastModified = 3,
  Color = 4,
}

export const allAnnotationsSortFields = Object.keys(AnnotationsSortField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as AnnotationsSortField[];

export type AnnotationsFilter = FilterV2<AnnotationsFilterField, AnnotationsSortField>;
