import {StatCount} from "./stat-count";

export interface Breakdown<T> {
  data: StatCount<T>[],
  total: number,
  totalOptions: number,
  missing: number,
}
