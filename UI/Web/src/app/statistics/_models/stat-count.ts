import {MangaFormat} from "../../_models/manga-format";

export interface StatCount<T> {
    value: T;
    count: number;
}

export interface StatCountWithFormat<T> {
  value: T;
  count: number;
  format: MangaFormat;
}
