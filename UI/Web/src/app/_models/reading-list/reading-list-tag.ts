import {BaseTag} from "../tag";

export interface ReadingListTag extends BaseTag {
  id: number;
  title: string;
  normalizedTitle: string;
}
