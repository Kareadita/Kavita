import {FilterV2} from "./filter-v2";
import {allEnums} from "../../../_helpers/enum";


export enum AnnotationsFilterField {
  Owner = 1,
  Library = 2,
  Spoiler = 3,
  HighlightSlots = 4,
  Selection = 5,
  Comment = 6,
  Series = 7,
  Likes = 8,
  LikedBy = 9,
}

export const allAnnotationsFilterFields = allEnums(AnnotationsFilterField);

export enum AnnotationsSortField {
  Owner = 1,
  Created = 2,
  LastModified = 3,
  Color = 4,
}

export const allAnnotationsSortFields = allEnums(AnnotationsSortField);

export type AnnotationsFilter = FilterV2<AnnotationsFilterField, AnnotationsSortField>;
